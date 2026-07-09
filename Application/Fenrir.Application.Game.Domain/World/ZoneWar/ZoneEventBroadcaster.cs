using System.Buffers;
using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
///     <para>
///         CROSS-SHARD RELAY (rvr-siege): the "no inter-process hop left to model" claim above only holds
///         WITHIN one shard -- Fenrir shards <see cref="ZoneRegistry" /> disjointly by map, so
///         <see cref="BroadcastToEveryZone{TPacket}" /> (and every reactive
///         <see cref="TribeGuardSpawner" />/<see cref="TribeSymbolSpawner" /> forced-resummon call below) only
///         ever reaches THIS shard's own hosted maps/players, unlike the legacy's single ts25center process
///         relaying to literally every connected zone process cluster-wide. <see cref="AnnounceZone038Winner" />/
///         <see cref="AnnounceTribeSymbolBattleCountdown" />/<see cref="AnnounceTribeSymbolBattleStarted" />/
///         <see cref="AnnounceSymbolResolved" />/<see cref="AnnounceTribeSymbolBattleEnded" />/
///         <see cref="AnnounceAllianceOffer" />/<see cref="AnnounceAllianceDissolved" /> (tSort 38/39/40/42/45/
///         46/47) additionally enqueue onto <see cref="IRvrSiegeEventRelayQueue" /> (SQL-mediated fan-out, same
///         precedent as <c>GuildTribeBroadcastRelayHost</c>) so every OTHER live shard's own
///         <c>RvrSiegeEventRelayHost</c> poll cycle reproduces the same local broadcast plus reactive guard/
///         symbol resummon through <see cref="ApplyRelayedEvent" /> -- without this, a tribe-symbol-battle
///         start on shard A would never spawn the physical symbol NPCs or notify players on shard B at all.
///         <see cref="ApplyRelayedEvent" /> ALSO re-applies the matching <see cref="WorldStateService" /> setter
///         (decoded back out of the relayed payload) rather than waiting on that class's own DB-backed
///         <c>ReconcileAsync</c> poll to converge it: every setter involved is a plain overwrite, so replaying
///         it is always idempotent-safe, and skipping it would leave same-call reactive effects that read
///         <see cref="WorldStateService.World" /> straight back out (in particular
///         <see cref="TribeGuardSpawner.ForceZone038WinnerResummon" />) racing that poll interval instead of
///         seeing the correct value immediately. Zone049 (sub-codes 1-9) is the sibling half of this same
///         relay, owned by <see cref="ZoneCenterBroadcastIngestor" /> instead (see that class's own remarks)
///         since this class only ever covers 38/39/40/42/45/46/47.
///     </para>
/// </summary>
public sealed class ZoneEventBroadcaster(
    WorldStateService worldState,
    ZoneRegistry zones,
    ILogger<ZoneEventBroadcaster> logger,
    TribeGuardSpawner? guardSpawner = null,
    TribeSymbolSpawner? symbolSpawner = null,
    ZoneCenterSiegeState? siegeState = null,
    IRvrSiegeEventRelayQueue? relayQueue = null,
    IOptions<GameServerOptions>? gameOptions = null)
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
        var data = Broadcast(38, tribeId);

        if (guardSpawner is not null)
            foreach (var zone in zones.Zones)
                guardSpawner.ForceZone038WinnerResummon(zone);

        EnqueueForOtherShards(38, data);
    }

    /// <summary>
    ///     Broadcast case 39 ("hsb countdown", <c>S07_MyGame08.cpp:219-222</c>): forces the ordinary guard pool
    ///     to resummon on every zone. No <see cref="WorldStateService" /> field changes for this case -- it is
    ///     purely a client notice plus a forced guard re-evaluation.
    /// </summary>
    public void AnnounceTribeSymbolBattleCountdown()
    {
        var data = Broadcast(39);

        if (guardSpawner is not null)
            foreach (var zone in zones.Zones)
                guardSpawner.ForceOrdinaryResummon(zone);

        EnqueueForOtherShards(39, data);
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
        var data = Broadcast(40);

        if (symbolSpawner is not null)
            foreach (var zone in zones.Zones)
                symbolSpawner.EvaluateNow(zone);

        if (guardSpawner is not null)
            foreach (var zone in zones.Zones)
                guardSpawner.ForceOrdinaryResummon(zone);

        EnqueueForOtherShards(40, data);

        logger.LogInformation("Tribe symbol battle opened; every tribe reset to its own symbol");
    }

    /// <summary>tSort 45: the tribe-symbol (HSB) battle window closes.</summary>
    public void AnnounceTribeSymbolBattleEnded()
    {
        using var scope = logger.BeginScope("SymbolBattle {SymbolBattlePhase}", "Ended");

        worldState.EndTribeSymbolBattle();
        var data = Broadcast(45);
        EnqueueForOtherShards(45, data);

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

        var data = Broadcast(42, symbolIndex, winnerTribeId);
        EnqueueForOtherShards(42, data);

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
        var data = Broadcast(46, fromTribeId, toTribeId, isAccepted ? 1 : 0);
        EnqueueForOtherShards(46, data);
    }

    /// <summary>
    ///     tSort 47: an existing alliance between <paramref name="tribeA" /> and <paramref name="tribeB" /> is
    ///     dissolved -- either by <see cref="ZoneWar.AllianceDiplomacyCeremony" />'s already-allied-negotiation
    ///     completing, or by the separate alliance-stone-destruction mechanism (S04_MyWork02.cpp:427-448).
    /// </summary>
    public void AnnounceAllianceDissolved(byte tribeA, byte tribeB)
    {
        worldState.DissolveAlliance(tribeA, tribeB);
        var data = Broadcast(47, tribeA, tribeB);
        EnqueueForOtherShards(47, data);
    }

    /// <summary>
    ///     tSort 401 (<c>bAttackMonsterSymbol</c>, S07_MyGame01.cpp:2622-2651): the current monster-symbol
    ///     holder has kept the neutral, monster-guarded battle symbol for the configured delay -- a bare
    ///     notice, no payload beyond the opcode itself, fired at most once per holding period (see
    ///     <see cref="MonsterSymbolAttackWindowTracker" />'s own remarks). Does not itself mutate
    ///     <see cref="WorldStateService" />: unlike every other <c>Announce*</c> method above, this is a pure
    ///     notification with no world-state side effect of its own.
    /// </summary>
    public void AnnounceMonsterSymbolAttackWindow()
    {
        Broadcast(401);
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

        BroadcastToEveryZone(in response);
    }

    /// <summary>
    ///     FFA-335 event codes 1501-1507 -- the six phase-transition announcements
    ///     <see cref="Zone335FfaEventCycleSystem" />'s own tick drives. Every method here (other than
    ///     <see cref="AnnounceFfaCountdown" />, which the source contract states leaves the shared scalar at 0
    ///     throughout) both mutates <see cref="ZoneCenterSiegeState.Zone335" /> and broadcasts the matching
    ///     event code cluster-wide in one call -- collapsing the legacy's separate zone-&gt;center relay,
    ///     center-&gt;every-zone fan-out, and zone-&gt;local-client broadcast into a single shared-memory write plus
    ///     one fan-out, same "no inter-process hop left to model" posture as every other <c>Announce*</c> method
    ///     on this class. <paramref name="siegeState" /> is optional purely so every existing positional
    ///     constructor call site (production DI resolves it automatically; only hand-written test call sites
    ///     that don't need these six methods may omit it) keeps compiling -- a production instance always has
    ///     it, since <c>ZoneCenterSiegeState</c> is registered as a singleton alongside this class.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25center/S04_MyWork02.cpp:1125-1150 (full 1501-1507 table, verified this task:
    ///     1501 no-op, 1502 sets the shared scalar to 1, 1503 to 2, 1504 to 3, 1505 to 4, 1506 to 5, 1507 back to
    ///     0) ; :1213-1221 (relay to every connected zone-server socket) ; Server/ts25zone/S07_MyGame08.cpp:
    ///     1350-1352 (unconditional local re-broadcast on every receiving zone process -- the mechanism making
    ///     these effectively server-wide, reaching players regardless of which map they're on) ;
    ///     Server/ts25zone/S07_MyGame01.cpp:10794-10804 (1501's remaining-minutes-of-countdown payload) ;
    ///     :10833-10850 (1504's battle-timer payload).
    /// </remarks>
    public void AnnounceFfaCountdown(int minutesRemaining)
    {
        Broadcast(1501, minutesRemaining);
    }

    public void AnnounceFfaGateOpen()
    {
        siegeState?.SetZone335(1);
        Broadcast(1502);
    }

    public void AnnounceFfaEntranceOpen()
    {
        siegeState?.SetZone335(2);
        Broadcast(1503);
    }

    public void AnnounceFfaBattleStart(int battleTimerLegacyTicks)
    {
        siegeState?.SetZone335(3);
        Broadcast(1504, battleTimerLegacyTicks);
    }

    public void AnnounceFfaBattleEnd()
    {
        siegeState?.SetZone335(4);
        Broadcast(1505);
    }

    /// <summary>
    ///     Also reused, idempotently, for phase 9's own once-per-minute re-announcement while the shared scalar
    ///     already reads 5 -- setting it to 5 again there is a harmless no-op.
    /// </summary>
    public void AnnounceFfaClosedNotice()
    {
        siegeState?.SetZone335(5);
        Broadcast(1506);
    }

    public void AnnounceFfaReset()
    {
        siegeState?.ResetZone335();
        Broadcast(1507);
    }

    /// <summary>
    ///     tSort 659: Valley of the Deceased (Zone 200/297/298/299) gate-open countdown -- fires once per elapsed
    ///     real minute while the value is still above zero, five times per cycle (5,4,3,2,1). Payload = the
    ///     current remaining-count integer.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:11246 (the live send site -- the input finding's own
    ///     :11192 is dead code, see this cluster's behavior contract, Cross-cutting note 3) ; delivery mechanism
    ///     shared by every <c>AnnounceValleyWar*</c> method below: Server/ts25zone/H06_MyUpperCom.h:109-121,
    ///     Server/ts25zone/UpperCom/S06_MyUpperCom02.cpp:446-480, Server/Header/Protocol/DEFINE.h:377-381,
    ///     Server/Header/Protocol/ZONE.h:20-24,36-37,918-922,1579-1580, Server/ts25center/S04_MyWork02.cpp:160-168,
    ///     1043-1076,1214-1221, Server/ts25zone/S07_MyGame08.cpp:10,1126-1155,1350-1352,
    ///     Server/ts25zone/S05_MyTransfer.cpp:1138-1143, Server/ts25zone/S07_MyGame03.cpp:777-794.
    /// </remarks>
    public void AnnounceValleyWarGateCountdown(int remainingCount)
    {
        Broadcast(659, remainingCount);
    }

    /// <summary>tSort 660: Valley of the Deceased gate opened, no payload.</summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:11232 (the live send site -- the input finding's own
    ///     :11211 is dead code, see this cluster's behavior contract, Cross-cutting note 3).
    /// </remarks>
    public void AnnounceValleyWarGateOpened()
    {
        Broadcast(660);
    }

    /// <summary>tSort 662: Valley of the Deceased gate closed, no payload.</summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:11252-11268.</remarks>
    public void AnnounceValleyWarGateClosed()
    {
        Broadcast(662);
    }

    /// <summary>tSort 663: Valley of the Deceased door opened (kill-race phase begins), no payload.</summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:11269-11304.</remarks>
    public void AnnounceValleyWarDoorOpened()
    {
        Broadcast(663);
    }

    /// <summary>
    ///     tSort 666: Valley of the Deceased "last door opened" / kill-race winner, payload = the winning
    ///     tribe's index.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:11380-11433.</remarks>
    public void AnnounceValleyWarTribeWin(byte winningTribe)
    {
        Broadcast(666, winningTribe);
    }

    /// <summary>tSort 667: Valley of the Deceased battle-scroll deletion, no payload.</summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:11435-11450.</remarks>
    public void AnnounceValleyWarBattleScrollDeleted()
    {
        Broadcast(667);
    }

    /// <summary>tSort 668: Valley of the Deceased boss defeated ("you won"), no payload.</summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:11476-11532.</remarks>
    public void AnnounceValleyWarBossDefeated()
    {
        Broadcast(668);
    }

    /// <summary>
    ///     tSort 669: Valley of the Deceased "return to town" -- reused verbatim for three distinct outcomes
    ///     (empty map/kill-race timeout, boss-window timeout, post-win cooldown end); the client does not
    ///     distinguish between them on this discriminator alone, matching the source contract exactly.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:11305-11343,11357-11379 (kill-race outcomes) ;
    ///     :11451-11475 (boss-window timeout) ; :11534-11557 (post-win cooldown end).
    /// </remarks>
    public void AnnounceValleyWarReturnToTown()
    {
        Broadcast(669);
    }

    /// <summary>
    ///     Returns the exact <see cref="ZoneEventInfoResponse.Data" /> bytes this call broadcast, so the seven
    ///     rvr-siege producer methods (38/39/40/42/45/46/47) can hand the identical payload to
    ///     <see cref="EnqueueForOtherShards" /> without re-encoding it a second time.
    /// </summary>
    private byte[] Broadcast(int sort, params ReadOnlySpan<int> fields)
    {
        var data = new byte[DataSize];
        for (var i = 0; i < fields.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i * 4), fields[i]);

        var response = new ZoneEventInfoResponse { Sort = sort, Data = data };

        BroadcastToEveryZone(in response);

        return data;
    }

    /// <summary>
    ///     rvr-siege cross-shard leg for the seven <c>Announce*</c> methods that opt in (38/39/40/42/45/46/47) --
    ///     see class remarks, CROSS-SHARD RELAY. A no-op when <see cref="relayQueue" /> is unset (every existing
    ///     hand-written test call site).
    /// </summary>
    private void EnqueueForOtherShards(int sort, byte[] data)
    {
        if (relayQueue is null)
            return;

        var shardId = gameOptions?.Value.ShardId ?? 0;
        relayQueue.Enqueue(new RvrSiegeEventRelayEntry(shardId, sort, data));
    }

    /// <summary>
    ///     The replay entry point a sibling shard's own <c>RvrSiegeEventRelayHost</c> calls for a tSort
    ///     38/39/40/42/45/46/47 row this shard did NOT originate: re-applies the identical
    ///     <see cref="WorldStateService" /> mutation the origin shard's own <c>Announce*</c> method applied
    ///     (decoded back out of the same <paramref name="data" /> bytes that method encoded), reproduces the
    ///     local broadcast, and reproduces whichever reactive guard/symbol resummon that method performed --
    ///     WITHOUT re-enqueueing (that would echo the row back out to every other shard forever).
    ///     <paramref name="data" /> is authoritative here (not "already converged, so skip it"): every one of
    ///     these <see cref="WorldStateService" /> setters is a plain overwrite, never an additive delta, so
    ///     replaying it is always idempotent-safe against that class's own DB-backed
    ///     <c>WorldStateService.ReconcileAsync</c> convergence -- and skipping it would leave this shard's own
    ///     reactive effects (in particular <see cref="TribeGuardSpawner.ForceZone038WinnerResummon" />, which
    ///     reads <see cref="WorldStateService.World" />'s <c>Zone038WinTribe</c> back out immediately) racing
    ///     that poll-interval-bounded convergence instead of seeing the correct value on this same call.
    /// </summary>
    public void ApplyRelayedEvent(int sort, ReadOnlySpan<byte> data)
    {
        if (data.Length != DataSize)
            throw new ArgumentException($"RvR-siege relay payload must be exactly {DataSize} bytes.", nameof(data));

        switch (sort)
        {
            case 38:
                worldState.SetZone038Winner((byte)ReadInt32(data, 0));
                break;

            case 40:
                worldState.StartTribeSymbolBattle();
                break;

            case 42:
                var symbolIndex = (byte)ReadInt32(data, 0);
                var winnerTribeId = (byte)ReadInt32(data, 4);
                if (symbolIndex == WorldStateService.TribeCount)
                    worldState.ResolveMonsterSymbol(winnerTribeId);
                else
                    worldState.ResolveTribeSymbol(symbolIndex, winnerTribeId);
                break;

            case 45:
                worldState.EndTribeSymbolBattle();
                break;

            case 46:
                worldState.SetAllianceOffer((byte)ReadInt32(data, 0), (byte)ReadInt32(data, 4),
                    ReadInt32(data, 8) != 0);
                break;

            case 47:
                worldState.DissolveAlliance((byte)ReadInt32(data, 0), (byte)ReadInt32(data, 4));
                break;

            // 39 carries no WorldStateService mutation on the origin side either (AnnounceTribeSymbolBattleCountdown's
            // own remarks) -- nothing to replay here beyond the shared broadcast/reactive-effect steps below.
        }

        var response = new ZoneEventInfoResponse { Sort = sort, Data = data.ToArray() };
        BroadcastToEveryZone(in response);

        switch (sort)
        {
            case 38:
                if (guardSpawner is not null)
                    foreach (var zone in zones.Zones)
                        guardSpawner.ForceZone038WinnerResummon(zone);
                break;

            case 39:
                if (guardSpawner is not null)
                    foreach (var zone in zones.Zones)
                        guardSpawner.ForceOrdinaryResummon(zone);
                break;

            case 40:
                if (symbolSpawner is not null)
                    foreach (var zone in zones.Zones)
                        symbolSpawner.EvaluateNow(zone);

                if (guardSpawner is not null)
                    foreach (var zone in zones.Zones)
                        guardSpawner.ForceOrdinaryResummon(zone);
                break;

            // 42/45/46/47: no reactive guard/symbol effect on the zone side -- the shared local broadcast
            // above (plus the WorldStateService replay in the first switch) is the whole of this replay for
            // these four sorts.
        }
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    }

    /// <summary>
    ///     Serialize-once, cluster-wide fan-out: the frame is encoded exactly once and copied into every
    ///     connected player's own transport across every zone this shard hosts -- same rent-once/write-once/
    ///     copy-N-times idiom every other Zone broadcast helper already uses (e.g.
    ///     <c>Zone.PlayerLifecycle.BroadcastAvatarAction</c>), with a per-recipient failure isolated (logged,
    ///     skipped) instead of left to abort the rest of the fan-out. Without this, a single recipient whose
    ///     transport pipe writer has already completed (an ordinary disconnect race: the player is still
    ///     present in <see cref="Zone.Players" /> until that player's own zone next drains its Leave command)
    ///     throws out of <see cref="ClientSession.Send{TPacket}" /> uncaught by a bare loop -- silently cutting
    ///     off delivery to every zone/player still left to visit in the SAME call, for every <c>Announce*</c>
    ///     method on this class (all of which funnel through here).
    /// </summary>
    private void BroadcastToEveryZone<TPacket>(in TPacket response) where TPacket : struct, IOutgoingPacket
    {
        var total = FrameWriter.FrameSizeOf<TPacket>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            foreach (var zone in zones.Zones)
            foreach (var player in zone.Players)
                try
                {
                    if (player.Session is ClientSession clientSession)
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Cluster-wide RvR broadcast to character {RecipientId} (zone {MapId}) failed",
                        player.CharacterId, zone.MapId);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
