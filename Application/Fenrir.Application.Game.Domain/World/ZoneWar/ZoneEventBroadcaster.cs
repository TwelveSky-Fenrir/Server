using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Modern replacement for the legacy op94 hop (ZCP_BROADCAST_INFO_RECV, ZC_BROADCAST_INFO_RECV a.k.a.
///     <see cref="ZoneEventInfoResponse" />): every legacy ts25zone process learned of a
///     <c>mWorldInfo</c>/<c>mTribeInfo</c> mutation from a separate ts25center hop
///     (ZCP_ZONE_BROADCAST_FOR_CENTER_SEND -&gt; <c>MyGame::ProcessForBroadcastInfo</c>) before relaying the
///     client-facing notice onward. Fenrir runs every zone in this one process against the one shared
///     <see cref="WorldStateService" /> instance, so there is no inter-process hop left to model -- this
///     class collapses "mutate the shared state" and "tell every connected client what changed" into a
///     single call.
///     <para>
///         Only the RvR sorts <see cref="WorldStateService" /> actually models are covered here
///         (S07_MyGame08.cpp cases 38/40/42/45/46 -- see that class's own remarks for which legacy fields have
///         no backing column yet). The many other legacy sorts sharing this same wire opcode (guild notices,
///         the numbered zone-siege state machines) belong to their own features.
///     </para>
/// </summary>
public sealed class ZoneEventBroadcaster(
    WorldStateService worldState,
    ZoneRegistry zones,
    ILogger<ZoneEventBroadcaster> logger,
    TribeGuardSpawner? guardSpawner = null,
    TribeSymbolSpawner? symbolSpawner = null)
{
    private const int DataSize = 130;

    /// <summary>
    ///     tSort 38 (S04_MyWork02.cpp case 38 equivalent): a tribe has just decided Zone038. Also forces the
    ///     zone038-winner guard pool to resummon on every zone, mirroring <c>SummonGuard(TRUE, TRUE)</c> being
    ///     called directly at this exact state transition (<c>S07_MyGame01.cpp:4200-4202</c>).
    /// </summary>
    public void AnnounceZone038Winner(byte tribeId)
    {
        worldState.SetZone038Winner(tribeId);
        Broadcast(38, tribeId);

        if (guardSpawner is not null)
            foreach (var zone in zones.Zones)
                guardSpawner.ForceZone038WinnerResummon(zone);
    }

    /// <summary>
    ///     Broadcast case 39 ("hsb countdown", <c>S07_MyGame08.cpp:219-222</c>): forces the ordinary guard pool
    ///     to resummon on every zone. No <see cref="WorldStateService" /> field changes for this case -- it is
    ///     purely a client notice plus a forced guard re-evaluation.
    /// </summary>
    public void AnnounceTribeSymbolBattleCountdown()
    {
        Broadcast(39);

        if (guardSpawner is not null)
            foreach (var zone in zones.Zones)
                guardSpawner.ForceOrdinaryResummon(zone);
    }

    /// <summary>
    ///     tSort 40: the tribe-symbol (HSB) battle window opens; every tribe starts back on its own symbol.
    ///     Broadcast case 40 ("hsb start", <c>S07_MyGame08.cpp:223-231</c>) also calls <c>SummonTribeSymbol()</c>
    ///     then <c>SummonGuard(FALSE, TRUE)</c> on every zone -- reproduced here as an immediate symbol
    ///     re-evaluation followed by a forced ordinary-pool guard resummon.
    /// </summary>
    public void AnnounceTribeSymbolBattleStarted()
    {
        using var scope = logger.BeginScope("SymbolBattle {SymbolBattlePhase}", "Started");

        worldState.StartTribeSymbolBattle();
        Broadcast(40);

        if (symbolSpawner is not null)
            foreach (var zone in zones.Zones)
                symbolSpawner.EvaluateNow(zone);

        if (guardSpawner is not null)
            foreach (var zone in zones.Zones)
                guardSpawner.ForceOrdinaryResummon(zone);

        logger.LogInformation("Tribe symbol battle opened; every tribe reset to its own symbol");
    }

    /// <summary>tSort 45: the tribe-symbol (HSB) battle window closes.</summary>
    public void AnnounceTribeSymbolBattleEnded()
    {
        using var scope = logger.BeginScope("SymbolBattle {SymbolBattlePhase}", "Ended");

        worldState.EndTribeSymbolBattle();
        Broadcast(45);

        logger.LogInformation("Tribe symbol battle closed");
    }

    /// <summary>
    ///     tSort 42: one symbol slot is resolved. <paramref name="symbolIndex" /> 0-3 is a tribe's own slot
    ///     (<paramref name="winnerTribeId" /> only decides whether that tribe keeps it); 4 is the neutral,
    ///     monster-guarded slot.
    /// </summary>
    public void AnnounceSymbolResolved(byte symbolIndex, byte winnerTribeId)
    {
        using var scope = logger.BeginScope("SymbolBattle {SymbolIndex} {WinnerTribeId}", symbolIndex, winnerTribeId);

        if (symbolIndex == WorldStateService.TribeCount)
            worldState.ResolveMonsterSymbol(winnerTribeId);
        else
            worldState.ResolveTribeSymbol(symbolIndex, winnerTribeId);

        Broadcast(42, symbolIndex, winnerTribeId);

        logger.LogInformation("Symbol {SymbolIndex} resolved to tribe {WinnerTribeId}", symbolIndex, winnerTribeId);
    }

    /// <summary>
    ///     tSort 46: a tribe alliance offer is set. The legacy's 3-sort propose/accept/reject-with-date
    ///     distinction (S07_MyGame08.cpp cases 46/47/49) collapses onto <see cref="WorldStateService" />'s flat
    ///     (from, to, accepted) model -- this announces whichever of those this call just wrote.
    /// </summary>
    public void AnnounceAllianceOffer(byte fromTribeId, byte toTribeId, bool isAccepted)
    {
        worldState.SetAllianceOffer(fromTribeId, toTribeId, isAccepted);
        Broadcast(46, fromTribeId, toTribeId, isAccepted ? 1 : 0);
    }

    /// <summary>
    ///     tSort 47: an existing alliance between <paramref name="tribeA" /> and <paramref name="tribeB" /> is
    ///     dissolved -- either by <see cref="ZoneWar.AllianceDiplomacyCeremony" />'s already-allied-negotiation
    ///     completing, or by the separate alliance-stone-destruction mechanism (S04_MyWork02.cpp:427-448).
    /// </summary>
    public void AnnounceAllianceDissolved(byte tribeA, byte tribeB)
    {
        worldState.DissolveAlliance(tribeA, tribeB);
        Broadcast(47, tribeA, tribeB);
    }

    /// <summary>
    ///     tSort 1234: the flag-triggered, high-tribe-biased tribe-point recompute has just been persisted
    ///     successfully -- fan out the 4 freshly computed totals to every connected zone-server link exactly as
    ///     read (already applied to <see cref="WorldStateService" /> and the DB by the caller; this method does
    ///     not itself mutate any world state, unlike every other <c>Announce*</c> method on this class). Every
    ///     zone that receives this hop re-broadcasts the identical sort/payload to its own locally connected,
    ///     fully-logged-in, not-transferring player connections regardless of whether it recognizes tSort 1234
    ///     in its own dispatch (it does not -- there is no reachable case for it) -- reproduced here simply by
    ///     using the same <see cref="Broadcast" /> fan-out every other RvR sort in this class already uses,
    ///     since Fenrir collapses the legacy's separate center-&gt;zone and zone-&gt;client hops into one process.
    ///     A no-op with no error if no zone currently has any connected player (contract: "no connected zone
    ///     links at broadcast time").
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25center/S07_MyGame01.cpp:253-266 -- builds the tSort=1234 payload (4 totals at
    ///     the front of a zero-filled buffer) and calls <c>BroadcastZone</c> ; :278-288
    ///     (<c>MyGame::BroadcastZone</c>) -- fan-out to every connected zone-server link ;
    ///     Server/ts25zone/UpperCom/S06_MyUpperCom02.cpp:269-276,328-332 -- zone-side receipt and hand-off to
    ///     <c>MyGame::ProcessForBroadcastInfo</c> ; Server/ts25zone/S07_MyGame08.cpp:10-67,1310-1353 -- the
    ///     zone-side dispatch switch has no case for 1234 and no default label, yet unconditionally re-broadcasts
    ///     the same sort/payload to every local client afterward regardless of which case (if any) matched ;
    ///     Server/ts25zone/S07_MyGame03.cpp:777-794 (<c>MyUtil::BroadcastServer</c>) -- the local client fan-out
    ///     used by that unconditional trailer ; Server/Header/Protocol/DEFINE.h:69,377-381
    ///     (<c>USE_ITEM_LINK_V2</c> unconditional, <c>MAX_TRIBE_NUM</c> = 4) -- the 130-byte payload
    ///     (<see cref="DataSize" />) and the 4-total domain.
    /// </remarks>
    public void AnnounceTribePointTotals(IReadOnlyList<int> totals)
    {
        if (totals.Count != WorldStateService.TribeCount)
            throw new ArgumentException($"Expected exactly {WorldStateService.TribeCount} totals.", nameof(totals));

        Broadcast(1234, totals[0], totals[1], totals[2], totals[3]);
    }

    /// <summary>
    ///     Tower status broadcast (legacy tSort=752, <c>U_ZONE_BROADCAST_FOR_CENTER_SEND</c>) on upgrade-completion
    ///     and, separately, on destroy-completion -- see <see cref="Progression.TowerGuardianSystem" />'s own two
    ///     call sites. Neither the receiving zone's own notification-dispatch switch nor Center's own world-state
    ///     mutation switch has a dedicated branch for 752, so both fall through to their unconditional default
    ///     trailer: every connected client in the whole cluster learns the new tower state, not merely players in
    ///     the affected tower's own zone. Unlike every other <c>Announce*</c> method on this class, the
    ///     client-facing packet here is the already-implemented, dedicated <see cref="TowerStatusResponse" /> (its
    ///     own wire opcode) rather than the generic op94 <see cref="ZoneEventInfoResponse" /> sort family this
    ///     class's other methods use -- only the shard-wide fan-out PATTERN below is reused, not
    ///     <see cref="Broadcast" /> itself.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:13733 (upgrade-completion send,
    ///     <c>U_ZONE_BROADCAST_FOR_CENTER_SEND(752, mTowerIndex, mTowerInfo-&gt;mState1Tower[mTowerIndex])</c>) ;
    ///     :13759 (destroy-completion send, the identical call) ; Server/ts25zone/S07_MyGame08.cpp:1085-1093
    ///     (zone-side dispatch switch's neighboring cases 751/753/754/755, no case 752 anywhere in this switch) ;
    ///     :1349-1352 (the switch's unconditional default trailer -- relay onward + local-client broadcast).
    ///     The Center-side relay/persistence citation previously here (S04_MyWork02.cpp:1085-1093/:1213-1221)
    ///     did not hold up under re-verification -- those line ranges are ts25zone's own REGISTER_AVATAR_SEND/
    ///     CLIENT_OK_FOR_ZONE_SEND handlers, not a ts25center switch -- and has been removed rather than left
    ///     misattributed pending a fresh citation.
    /// </remarks>
    public void AnnounceTowerStatus(TowerWarState towerWar)
    {
        // BuildStatusSnapshot() takes ONE lock over the whole 12-tower batch, so no in-flight
        // BeginUpgrade/CompleteUpgrade from another zone's own tick thread can be observed half-applied
        // across the batch -- unlike looping GetPackedState(i) per-tower (each independently locked), which
        // can produce a torn snapshot mixing pre- and post-completion state across different towers in the
        // same broadcast frame. Same call Zone.Combat.cs's own BroadcastTowerStatus already uses.
        var response = towerWar.BuildStatusSnapshot();

        foreach (var zone in zones.Zones)
        foreach (var player in zone.Players)
            player.Session.Send(response);
    }

    private void Broadcast(int sort, params ReadOnlySpan<int> fields)
    {
        var data = new byte[DataSize];
        for (var i = 0; i < fields.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i * 4), fields[i]);

        var response = new ZoneEventInfoResponse { Sort = sort, Data = data };

        foreach (var zone in zones.Zones)
        foreach (var player in zone.Players)
            player.Session.Send(response);
    }
}
