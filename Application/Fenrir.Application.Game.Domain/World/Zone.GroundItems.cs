using System.Buffers;
using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    /// <summary>
    ///     Enqueued by <see cref="TryClaimGroundItem" /> (any thread) so the despawn broadcast (needs
    ///     <see cref="_grid" />, tick-thread-only) happens from the tick, never the claiming handler's thread.
    /// </summary>
    private readonly ConcurrentQueue<GroundItemEntity> _claimedGroundItemDespawns = new();

    /// <summary>Tick-owned only -- see <see cref="_groundItems" />'s remarks.</summary>
    private readonly Dictionary<int, TimeSpan> _groundItemLastRebroadcast = new();

    // Populated/expired only by this zone's own tick; claimed (removed) via an atomic compare-and-remove
    // callable from any thread -- see TryClaimGroundItem's remarks for why pickup is the one exception.
    private readonly ConcurrentDictionary<int, GroundItemEntity> _groundItems = new();

    private int _groundItemServerIndexSeed;

    /// <summary>
    ///     Reusable scratch buffer for <see cref="BroadcastGroundItemAction" />'s recipient list -- same
    ///     non-allocating shape and reuse justification as <c>Zone.PlayerLifecycle</c>'s
    ///     <c>_rebroadcastNeighborScratch</c> / <c>Zone.Monsters</c>' <c>_monsterBroadcastNeighborScratch</c>:
    ///     single tick thread, cleared immediately before each call, never read after the immediately-following
    ///     send loop returns. Replaces a per-call <c>AoiGrid.Neighbors(cell).ToArray()</c> (iterator + LINQ
    ///     buffer) that used to run once per due ground item -- during a keep-alive burst, once per due item in
    ///     that SAME tick, not once per zone.
    /// </summary>
    private readonly List<int> _groundItemBroadcastNeighborScratch = [];

    private int _groundItemUniqueNumberSeed;

    public int GroundItemCount => _groundItems.Count;

    /// <summary>
    ///     Tick-owned caller only (<see cref="Monsters.MonsterSpawnScheduler" />). <paramref name="instanceId" />
    ///     is Side effect #3's Zone-241 drop tag (Server/ts25zone/S07_MyGame03.cpp:599) -- null for every
    ///     ordinary drop.
    /// </summary>
    public void SpawnGroundItem(int itemId, int quantity, float posX, float posY, float posZ, string master,
        string partyName, int dropSort, int? instanceId = null)
    {
        var index = Interlocked.Increment(ref _groundItemServerIndexSeed);
        var uniqueNumber = unchecked((uint)Interlocked.Increment(ref _groundItemUniqueNumberSeed));

        var entity = new GroundItemEntity(index, uniqueNumber, itemId, quantity, 0, 0, posX,
            posY, posZ, TruncateName(master), TruncateName(partyName), dropSort, _clock, 0,
            0, 0, instanceId);

        _groundItems[index] = entity;

        // Staggered, not a plain "= _clock": a single kill can drop several items in the same ProcessDeath
        // call (public item + boss-tier items + generic-tier items), all reaching this method within the same
        // Zone.Tick and reading the exact same _clock value -- same thundering-herd root cause as
        // Zone.SpawnMonster's own stagger, just at a smaller per-kill scale. See
        // SimulationClock.RebroadcastStaggerOffset's own remarks.
        _groundItemLastRebroadcast[index] =
            _clock - SimulationClock.RebroadcastStaggerOffset(index, SimulationClock.GroundItemRebroadcastInterval);
        BroadcastGroundItemAction(entity, 1); // action=1 at creation
    }

    private static string TruncateName(string name)
    {
        return name.Length <= 13 ? name : name[..13]; // MAX_AVATAR_NAME_LENGTH
    }

    /// <summary>
    ///     Atomically claims (removes) a ground item for pickup -- callable from any thread. Ownership window
    ///     and distance are checked against immutable snapshot fields, so only the removal itself needs to be
    ///     atomic: <see cref="ConcurrentDictionary{TKey,TValue}" />'s compare-and-remove on the snapshot just
    ///     read gives "first claimant wins" with no custom locking. The despawn broadcast is NOT done here (it
    ///     needs <see cref="_grid" />, tick-thread-only) -- see <see cref="_claimedGroundItemDespawns" />.
    /// </summary>
    public GroundItemClaimOutcome TryClaimGroundItem(int serverIndex, uint expectedUniqueNumber, string claimantName,
        string? claimantPartyName, float claimantX, float claimantY, float claimantZ, out GroundItemEntity? item,
        int? claimantInstanceId = null)
    {
        if (!_groundItems.TryGetValue(serverIndex, out var snapshot) ||
            snapshot.UniqueNumber != expectedUniqueNumber)
        {
            item = null;
            return GroundItemClaimOutcome.NotFound;
        }

        // Side effect #4's pickup gate (Server/ts25zone/S04_MyWork05.cpp:289-297): a mismatch is silently
        // refused, indistinguishable on the wire from "item no longer there" -- reuses NotFound rather than a
        // dedicated outcome, matching that documented collapse exactly.
        if (!IsVisibleAcrossDungeonInstance(snapshot.InstanceId, claimantInstanceId))
        {
            item = null;
            return GroundItemClaimOutcome.NotFound;
        }

        if (!snapshot.IsClaimableBy(claimantName, claimantPartyName, _clock))
        {
            item = null;
            return GroundItemClaimOutcome.NotOwned;
        }

        // ITEM_OBJECT::CheckPossibleGetItem (S07_MyGame06.cpp:63): full 3D distance, not XZ-only.
        var dx = snapshot.PosX - claimantX;
        var dy = snapshot.PosY - claimantY;
        var dz = snapshot.PosZ - claimantZ;
        if (dx * dx + dy * dy + dz * dz >
            GroundItemPickupPolicy.MaxPickupDistance * GroundItemPickupPolicy.MaxPickupDistance)
        {
            item = null;
            return GroundItemClaimOutcome.TooFar;
        }

        if (!((ICollection<KeyValuePair<int, GroundItemEntity>>)_groundItems).Remove(
                new KeyValuePair<int, GroundItemEntity>(serverIndex, snapshot)))
        {
            item = null;
            return GroundItemClaimOutcome.NotFound; // lost the race to a concurrent claimant
        }

        _claimedGroundItemDespawns.Enqueue(snapshot);
        item = snapshot;
        return GroundItemClaimOutcome.Success;
    }

    /// <summary>Keep-alive rebroadcast for ground items, 5 s cadence.</summary>
    private void RebroadcastGroundItems()
    {
        foreach (var (index, item) in _groundItems)
        {
            var last = _groundItemLastRebroadcast.TryGetValue(index, out var t) ? t : TimeSpan.MinValue;
            if (_clock - last < SimulationClock.GroundItemRebroadcastInterval)
                continue;

            _groundItemLastRebroadcast[index] = _clock;
            BroadcastGroundItemAction(item, 0);
        }
    }

    /// <summary>60 s lifetime sweep -- despawns and broadcasts every expired ground item still present.</summary>
    private void ExpireGroundItems()
    {
        List<(int Index, GroundItemEntity Item)>? expired = null;
        foreach (var (index, item) in _groundItems)
            if (item.IsExpired(_clock))
                (expired ??= []).Add((index, item));

        if (expired is null)
            return;

        foreach (var (index, item) in expired)
            if (_groundItems.TryRemove(index, out _))
            {
                _groundItemLastRebroadcast.Remove(index);
                BroadcastGroundItemAction(item, 3);
            }
    }

    private void DrainClaimedGroundItemDespawns()
    {
        while (_claimedGroundItemDespawns.TryDequeue(out var item))
        {
            _groundItemLastRebroadcast.Remove(item.ServerIndex);
            BroadcastGroundItemAction(item, 3);
        }
    }

    /// <summary>
    ///     Serialize-once broadcast for ground-item replication -- same pattern as <see cref="BroadcastAvatarAction" />,
    ///     including the same <see cref="IsReviveHackBroadcastSuppressed" /> per-recipient gate (see that method's
    ///     own remarks for the legacy citations establishing both broadcast families route through the same
    ///     MyUtil::Broadcast11 primitive).
    ///     <see cref="AoiGrid.HasAnyNeighbor" /> pre-checks emptiness before paying for
    ///     <see cref="_groundItemBroadcastNeighborScratch" />'s scan; see that field's own remarks for why this
    ///     is a reused non-allocating buffer rather than the enumerable-returning, iterator-plus-LINQ-<c>ToArray()</c>
    ///     overload of <c>AoiGrid.Neighbors</c>.
    /// </summary>
    private void BroadcastGroundItemAction(GroundItemEntity item, int checkChangeActionState)
    {
        var cell = _grid.CellOf(item.PosX, item.PosZ);
        if (!_grid.HasAnyNeighbor(cell))
            return;

        _groundItemBroadcastNeighborScratch.Clear();
        _grid.Neighbors(_groundItemBroadcastNeighborScratch, cell, item.PosX, item.PosY, item.PosZ);
        var packet = BuildItemActionRecv(item, checkChangeActionState);
        var total = FrameWriter.FrameSizeOf<GroundItemReplicationResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in packet, span);

            foreach (var id in _groundItemBroadcastNeighborScratch)
                try
                {
                    if (_players.TryGetValue(id, out var recipient) &&
                        recipient.Session is ClientSession clientSession &&
                        IsVisibleAcrossDungeonInstance(item.InstanceId, recipient.DungeonInstanceId) &&
                        !IsReviveHackBroadcastSuppressed(recipient))
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} ground-item broadcast to character {RecipientId} failed",
                        MapId, id);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static GroundItemReplicationResponse BuildItemActionRecv(GroundItemEntity item,
        int checkChangeActionState)
    {
        return new GroundItemReplicationResponse
        {
            ServerIndex = item.ServerIndex,
            UniqueNumber = item.UniqueNumber,
            Data = new ObjectForItem
            {
                Index = item.ItemId,
                Quantity = item.Quantity,
                Value = item.Value,
                SerialNumber = item.SerialNumber,
                Location = [item.PosX, item.PosY, item.PosZ],
                Master = item.Master,
                PartyName = item.PartyName,
                DropSort = item.DropSort,
                CreateTime = 0,
                PresentTime = 0,
                CreateState = 1,
                SocketGem = [item.SocketGem1, item.SocketGem2, item.SocketGem3]
            },
            CheckChangeActionState = checkChangeActionState
        };
    }
}
