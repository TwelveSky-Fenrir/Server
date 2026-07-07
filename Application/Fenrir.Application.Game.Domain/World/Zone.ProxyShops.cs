using System.Buffers;
using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    // CharacterIds force-closed by this tick's expiry check, awaiting their durable ShopState=0 write.
    // Tick must stay fully synchronous, so the write is deferred to ProxyShopExpiryFlushHost -- same posture
    // as _pendingMoneyGrants/MonsterLootFlushHost.
    private readonly ConcurrentQueue<int> _pendingProxyShopCloses = new();

    // Keyed by CharacterId (also this shop's wire ServerIndex -- see ProxyShopBroadcastEntry's remarks).
    // A ConcurrentDictionary, not tick-owned-only: OpenShopStallService/CloseShopStallService register/remove
    // whole entries directly from a handler thread (never mutate an existing entry's fields), while only this
    // zone's own tick ever reads the table or writes LastBroadcastAt -- same split-ownership posture as _monsters.
    private readonly ConcurrentDictionary<int, ProxyShopBroadcastEntry> _proxyShops = new();

    public int ProxyShopCount => _proxyShops.Count;

    /// <summary>
    ///     Registers (or fully replaces) the periodic-broadcast entry for a newly opened or re-opened proxy
    ///     shop. Callable from any thread -- unlike the tick-owned <c>SpawnMonster</c>/ground-item drop path
    ///     (both able to fire their own "just appeared" broadcast inline because they already run on this
    ///     zone's tick), this method must stay broadcast-free and touch only the concurrent dictionary.
    ///     <see cref="ProxyShopBroadcastEntry.LastBroadcastAt" /> is stamped to the registering moment, the
    ///     same as <c>MonsterEntity.LastRebroadcastAt</c>/the ground item's own rebroadcast timestamp at
    ///     spawn: the shop's first keep-alive goes out once the ordinary
    ///     <see cref="SimulationClock.ProxyShopRebroadcastInterval" /> throttle next elapses for it, on the
    ///     same cadence as every other shop -- no invented instant-visibility special case (there is no cited
    ///     legacy behavior for one).
    /// </summary>
    public void RegisterProxyShop(ProxyShopBroadcastEntry entry)
    {
        entry.LastBroadcastAt = _clock;
        _proxyShops[entry.CharacterId] = entry;
    }

    /// <summary>Removes a shop from the periodic-broadcast table on an explicit player-driven close. Callable from any thread.</summary>
    public void RemoveProxyShop(int characterId)
    {
        _proxyShops.TryRemove(characterId, out _);
    }

    /// <summary>
    ///     Best-effort in-place expiration update for an already-registered proxy shop -- the Fenrir analogue
    ///     of the legacy's shared, cross-process, four-faction/500-slot-per-faction live shop registry update
    ///     (Server/Header/Protocol/DEFINE.h:309,369), used by the proxy-shop rental-extension consumable item
    ///     branch of CZ_USE_INVENTORY_ITEM_SEND. Callable from any thread. Returns <see langword="false" />
    ///     with no other effect if this zone holds no entry for <paramref name="characterId" /> -- most
    ///     commonly because the acting character is calling this from a different zone/shard than the one
    ///     hosting their shop's broadcast entry (only <see cref="ProxyShopZonePolicy.ZoneNumber" />'s zone
    ///     instance ever holds any), the Fenrir-sharded shape of the legacy's own "registry not currently
    ///     mapped by this process" skip -- nothing observable differs to the client either way.
    /// </summary>
    public bool TryUpdateProxyShopExpiration(int characterId, int newShopDate)
    {
        if (!_proxyShops.TryGetValue(characterId, out var entry))
            return false;

        entry.ShopDate = newShopDate;
        return true;
    }

    /// <summary>Callable from any thread; the only intended caller is <c>ProxyShopExpiryFlushHost</c>.</summary>
    public IReadOnlyList<int> DrainPendingProxyShopCloses()
    {
        if (_pendingProxyShopCloses.IsEmpty)
            return [];

        List<int>? closed = null;
        while (_pendingProxyShopCloses.TryDequeue(out var characterId))
            (closed ??= []).Add(characterId);

        return (IReadOnlyList<int>?)closed ?? [];
    }

    /// <summary>
    ///     Periodic proxy/deputy-shop sweep: expiration force-close checked unconditionally every tick, else
    ///     a per-shop 5 s throttle gates the radius keep-alive rebroadcast. Server/ts25zone/S07_MyGame01.cpp:2600-2607,
    ///     in context 2586-2608 (inside <c>MyGame::Logic</c>, declared :1739).
    /// </summary>
    /// <remarks>
    ///     Legacy gates the whole sweep on <c>IsValidZoneForProxyShopProcess</c>'s server-number allowlist
    ///     (Server/Header/mapcheck.h:130-148). Fenrir only ever registers a shop against
    ///     <see cref="ProxyShopZonePolicy.ZoneNumber" />'s own zone in the first place (every proxy-shop packet
    ///     handler is already gated to that single map), so <see cref="_proxyShops" /> is already empty on
    ///     every other zone; this early-return is belt-and-braces documentation of that same legacy
    ///     single-process invariant (see <see cref="ProxyShopZonePolicy" />'s remarks), not a live behavioral
    ///     dependency.
    ///     Expiration and the keep-alive rebroadcast are two independent conditions, not one shared gate:
    ///     ServerDocs/12_ts25zone/08_MyGame01_PartieA.md:107-108 (citing :2600-2604) describes the 5 s figure
    ///     as specifically the *broadcast* throttle ("mêmes throttles" as monsters/items), with the automatic
    ///     proxy-shop closure (<c>mShopDate &lt; ReturnNowDate()</c>) as an additional per-shop check layered
    ///     on top -- nothing in that citation makes closure itself wait for the broadcast throttle to also be
    ///     due. An expired shop is force-closed the first tick this sweep observes it, however small that
    ///     tick's elapsed time; only the ordinary "still here" keep-alive is throttled.
    /// </remarks>
    private void RebroadcastProxyShops()
    {
        if (MapId != ProxyShopZonePolicy.ZoneNumber || _proxyShops.IsEmpty)
            return;

        var today = GameDate.Today();

        foreach (var (characterId, entry) in _proxyShops)
        {
            if (entry.ShopDate < today)
            {
                // Force-close: tell nearby clients the stall is gone (action state 3) BEFORE dropping the
                // entry -- the broadcast needs entry.Pos*/OwnerName/ShopName, only available while it is still
                // in the table -- then queue the durable ShopState=0 write. Unconditional on the throttle:
                // see this method's remarks.
                if (_proxyShops.TryRemove(characterId, out _))
                {
                    BroadcastProxyShopState(entry, 3);
                    _pendingProxyShopCloses.Enqueue(characterId);
                }

                continue;
            }

            if (_clock - entry.LastBroadcastAt < SimulationClock.ProxyShopRebroadcastInterval)
                continue;

            entry.LastBroadcastAt = _clock;
            BroadcastProxyShopState(entry, 0);
        }
    }

    /// <summary>
    ///     ZC_DEPUTY_PSHOP_ACTION_RECV2 (Server/ts25zone/S05_MyTransfer.cpp:1709-1717): shop slot index, shop
    ///     unique id, full state block, action-state code. Radius-scoped via the AOI grid -- Fenrir's standing
    ///     analog (already used identically for monsters/ground items) to legacy <c>MyUtil::Broadcast11</c>'s
    ///     coarse-bucket-plus-fine-distance recipient filter (Server/ts25zone/S07_MyGame03.cpp:796-844).
    /// </summary>
    /// <param name="entry">The shop entry to broadcast -- read only, not mutated here.</param>
    /// <param name="checkChangeActionState">
    ///     0 for the periodic "still here" re-broadcast (per the packet's own doc comment, "no animation
    ///     replay"), or 3 for a force-close despawn -- confirmed as the same code ground items use for their
    ///     own despawn by <c>ProcessCloseProxyInfo</c>'s
    ///     <c>
    ///         mTRANSFER.B_DEPUTY_PSHOP_ACTION_RECV(tProxy-&gt;mIndex,
    ///         tProxy-&gt;mUniqueNumber, &amp;tProxy-&gt;mState, 3)
    ///     </c>
    ///     (Server/ts25zone/S07_MyGame09.cpp:570, in
    ///     context 559-577; ServerDocs/12_ts25zone/15_MyGame08_09_EventsCenter_ProxyShops.md:570,580).
    /// </param>
    /// <remarks>
    ///     <see cref="AoiGrid.HasAnyNeighbor" /> pre-checks emptiness before paying for
    ///     <see cref="AoiGrid.Neighbors" />'s iterator plus a LINQ <c>ToArray()</c> buffer -- see that method's
    ///     own remarks. Gated by the same <see cref="IsReviveHackBroadcastSuppressed" /> per-recipient check as
    ///     every other AOI broadcast this zone sends: both the periodic proxy-shop keep-alive
    ///     (<c>mUTIL.Broadcast11</c>, S07_MyGame01.cpp:2606) and the explicit close path
    ///     (S07_MyGame09.cpp:208-209) route through the same legacy <c>MyUtil::Broadcast11</c> primitive that
    ///     embeds this check -- see <see cref="IsReviveHackBroadcastSuppressed" />'s own remarks.
    /// </remarks>
    private void BroadcastProxyShopState(ProxyShopBroadcastEntry entry, int checkChangeActionState)
    {
        var cell = _grid.CellOf(entry.PosX, entry.PosZ);
        if (!_grid.HasAnyNeighbor(cell))
            return;

        var recipients = _grid.Neighbors(cell, entry.PosX, entry.PosY, entry.PosZ).ToArray();
        var packet = new ProxyShopStallStateResponse
        {
            ServerIndex = entry.CharacterId,
            UniqueNumber = entry.UniqueNumber,
            ProxyObject = new ProxyStateInfo
            {
                Location = [entry.PosX, entry.PosY, entry.PosZ],
                Name = entry.OwnerName,
                PshopName = entry.ShopName
            },
            CheckChangeActionState = checkChangeActionState
        };

        var total = FrameWriter.FrameSizeOf<ProxyShopStallStateResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in packet, span);

            foreach (var id in recipients)
                try
                {
                    if (_players.TryGetValue(id, out var recipient) &&
                        recipient.Session is ClientSession clientSession &&
                        !IsReviveHackBroadcastSuppressed(recipient))
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} proxy-shop broadcast to character {RecipientId} failed",
                        MapId, id);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
