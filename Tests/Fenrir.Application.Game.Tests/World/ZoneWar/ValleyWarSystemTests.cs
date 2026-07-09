using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="ValleyWarSystem" />: the client-notifying, per-zone-tick driver for
///     <see cref="ValleyWarSchedule" /> (Zone 200/297/298/299, "Valley of the Deceased") -- a DISTINCT
///     feature from <see cref="RegularWarSchedulerHost" />'s own Zone049 family (see
///     <see cref="RegularWarSchedulerHostTests" />, left entirely unchanged by this task). Every test here
///     verifies which broadcast reaches which zone's players: op94 events (<see cref="ZoneEventInfoResponse" />,
///     tSort 659-669) are cluster/shard-wide, reaching every zone this registry hosts, while the door/kill-race/
///     boss-window countdowns (opcodes 116/118) and the forced end-of-cycle disconnect are strictly zone-local
///     to the Valley map itself.
/// </summary>
public class ValleyWarSystemTests
{
    private const short ValleyMapId = 200; // one of ValleyWarMapCatalog's 4 configured maps
    private const short OtherMapId = 1; // NOT a configured Valley map -- the cluster-wide-reach witness

    private static (ValleyWarSystem System, ValleyWarKillRegistry KillRegistry) CreateSystem(ZoneRegistry registry)
    {
        var worldState = ZoneTestKit.CreateWorldState();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var killRegistry = new ValleyWarKillRegistry();
        var system = new ValleyWarSystem(killRegistry, LazyBroadcaster(broadcaster), LazyRegistry(registry),
            NullLogger<ValleyWarSystem>.Instance);
        return (system, killRegistry);
    }

    private static Lazy<ZoneEventBroadcaster> LazyBroadcaster(ZoneEventBroadcaster broadcaster)
    {
        return new Lazy<ZoneEventBroadcaster>(() => broadcaster);
    }

    private static Lazy<ZoneRegistry> LazyRegistry(ZoneRegistry registry)
    {
        return new Lazy<ZoneRegistry>(() => registry);
    }

    private static (Zone Zone, ZoneClientSession Session, FakeDuplexPipe Pipe) EnterPlayer(ZoneRegistry registry,
        short mapId, int characterId, byte tribe)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        registry[mapId].Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, mapId, tribe: tribe)));
        registry[mapId].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        return (registry[mapId], session, pipe);
    }

    /// <summary>Ticks Idle -> GateCountdown(5 announcements + open) -> GateOpen -> DoorPending, landing exactly
    /// on the tick the door opens and <see cref="ValleyWarPhase.KillRace" /> begins.</summary>
    private static void AdvanceToKillRaceStart(ValleyWarSystem system, ValleyWarKillRegistry killRegistry,
        Zone valleyZone)
    {
        system.Simulate(valleyZone,
            ValleyWarSchedule.IdleWaitTicks +
            (ValleyWarSchedule.GateCountdownStartValue + 1) * ValleyWarSchedule.GateCountdownIntervalTicks +
            ValleyWarSchedule.GateOpenTicks +
            ValleyWarSchedule.DoorPendingTicks);

        Assert.Equal(ValleyWarPhase.KillRace, killRegistry.GetOrCreate(valleyZone.MapId).Phase);
    }

    private static void AssertZoneEventInfo(byte[] frame, int expectedSort, int expectedFirstDataInt = 0)
    {
        Assert.Equal(FrameWriter.FrameSizeOf<ZoneEventInfoResponse>(), frame.Length);
        Assert.Equal(expectedSort, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1)));
        Assert.Equal(expectedFirstDataInt, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(5)));
    }

    [Fact]
    public void UnconfiguredMap_Simulate_IsANoOp_NeverBroadcasts()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([OtherMapId]);
        var (zone, _, pipe) = EnterPlayer(registry, OtherMapId, 1, 0);

        var (system, _) = CreateSystem(registry);
        system.Simulate(zone, ValleyWarSchedule.IdleWaitTicks * 2);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void GateCountdown_GateOpened_GateClosed_BroadcastClusterWide_ReachingAZoneThatIsNotEvenAValleyMap()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([ValleyMapId, OtherMapId]);
        var (valleyZone, _, valleyPipe) = EnterPlayer(registry, ValleyMapId, 1, 0);
        var (_, _, witnessPipe) = EnterPlayer(registry, OtherMapId, 2, 1);

        var (system, _) = CreateSystem(registry);

        // First gate-countdown announcement (tSort 659, payload 5) -- reaches BOTH zones' players, including
        // the witness zone, which never hosts a Valley of the Deceased map at all.
        system.Simulate(valleyZone, ValleyWarSchedule.IdleWaitTicks + ValleyWarSchedule.GateCountdownIntervalTicks);
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(valleyPipe), 659, 5);
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 659, 5);

        // Skip the remaining 4,3,2,1 announcements (covered by ValleyWarScheduleTests) straight to gate-open.
        system.Simulate(valleyZone, 4 * ValleyWarSchedule.GateCountdownIntervalTicks);
        ZoneTestKit.DrainOutbound(valleyPipe);
        ZoneTestKit.DrainOutbound(witnessPipe);

        system.Simulate(valleyZone, ValleyWarSchedule.GateCountdownIntervalTicks); // -> GateOpen (tSort 660)
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(valleyPipe), 660);
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 660);

        system.Simulate(valleyZone, ValleyWarSchedule.GateOpenTicks); // -> DoorPending (tSort 662, gate closed)
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(valleyPipe), 662);
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 662);
    }

    [Fact]
    public void DoorPendingCountdown_And_KillRaceQuotas_AreZoneLocalOnly_NeverReachOtherZones()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([ValleyMapId, OtherMapId]);
        var (valleyZone, _, valleyPipe) = EnterPlayer(registry, ValleyMapId, 1, 0);
        var (_, _, witnessPipe) = EnterPlayer(registry, OtherMapId, 2, 1);

        var (system, killRegistry) = CreateSystem(registry);

        // Advance to the start of DoorPending (Idle + full GateCountdown + GateOpen), draining the cluster-wide
        // events already covered by the test above.
        system.Simulate(valleyZone,
            ValleyWarSchedule.IdleWaitTicks +
            (ValleyWarSchedule.GateCountdownStartValue + 1) * ValleyWarSchedule.GateCountdownIntervalTicks +
            ValleyWarSchedule.GateOpenTicks);
        ZoneTestKit.DrainOutbound(valleyPipe);
        ZoneTestKit.DrainOutbound(witnessPipe);

        // The first 9 (of 10) door countdown sends -- opcode 116, zone-local only, one per elapsed real second.
        // The door hasn't opened yet, so no cluster-wide event is in flight either.
        system.Simulate(valleyZone, ValleyWarSchedule.DoorPendingTicks - 1);
        var earlyDoorFrames = ZoneTestKit.DrainOutbound(valleyPipe);
        Assert.Equal(9 * FrameWriter.FrameSizeOf<ZoneWar297StatusResponse>(), earlyDoorFrames.Length);
        Assert.Empty(ZoneTestKit.DrainOutbound(witnessPipe));

        // The 20th (last) tick: the 10th local door countdown AND the door-open transition land on the SAME
        // tick -- the former is zone-local only, the latter (tSort 663) is cluster-wide.
        system.Simulate(valleyZone, 1);
        Assert.Equal(ValleyWarPhase.KillRace, killRegistry.GetOrCreate(ValleyMapId).Phase);

        var finalValleyFrames = ZoneTestKit.DrainOutbound(valleyPipe);
        Assert.Equal(
            FrameWriter.FrameSizeOf<ZoneWar297StatusResponse>() + FrameWriter.FrameSizeOf<ZoneEventInfoResponse>(),
            finalValleyFrames.Length);
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 663); // door-open reaches the other zone too

        system.Simulate(valleyZone, 2); // 2 ticks -> first kill-race quota broadcast (opcode 118, every 2 ticks)
        var quotaFrame = ZoneTestKit.DrainOutbound(valleyPipe);
        Assert.Equal(FrameWriter.FrameSizeOf<ZoneWar297MonsterCountResponse>(), quotaFrame.Length);
        Assert.Empty(ZoneTestKit.DrainOutbound(witnessPipe));
    }

    [Fact]
    public void KillRace_MapReadsEmpty_EndsImmediately_BroadcastsReturnToTownClusterWide()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([ValleyMapId, OtherMapId]);
        var (valleyZone, valleySession, valleyPipe) = EnterPlayer(registry, ValleyMapId, 1, 0);
        var (_, _, witnessPipe) = EnterPlayer(registry, OtherMapId, 2, 1);

        var (system, killRegistry) = CreateSystem(registry);
        AdvanceToKillRaceStart(system, killRegistry, valleyZone);
        ZoneTestKit.DrainOutbound(valleyPipe);
        ZoneTestKit.DrainOutbound(witnessPipe);

        // The sole Valley-zone character is mid zone-transfer -- the "not mid-transfer" eligibility criterion
        // fails, so the map reads as empty on the very first KillRace tick.
        Assert.True(valleyZone.TryGetPlayer(1, out var solePlayer));
        solePlayer!.IsMovingZone = true;

        system.Simulate(valleyZone, 1);

        Assert.Equal(ValleyWarPhase.PreReset, killRegistry.GetOrCreate(ValleyMapId).Phase);
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 669);
        Assert.Null(valleySession.DisconnectReason); // not disconnected yet -- PreReset's own 1-minute wait hasn't elapsed
    }

    [Fact]
    public void KillRace_TribeWin_Then_ScrollDeleted_Then_BossDefeated_Then_ReturnToTown_AllBroadcastClusterWide()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([ValleyMapId, OtherMapId]);
        var (valleyZone, _, valleyPipe) = EnterPlayer(registry, ValleyMapId, 1, 2);
        var (_, _, witnessPipe) = EnterPlayer(registry, OtherMapId, 2, 0);

        var (system, killRegistry) = CreateSystem(registry);
        AdvanceToKillRaceStart(system, killRegistry, valleyZone);
        ZoneTestKit.DrainOutbound(valleyPipe);
        ZoneTestKit.DrainOutbound(witnessPipe);

        killRegistry.GetOrCreate(ValleyMapId).ForceZeroTribeQuota(2);

        system.Simulate(valleyZone, 1); // -> ScrollPending: tSort 666, payload = winning tribe (2)
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 666, 2);

        system.Simulate(valleyZone, ValleyWarSchedule.ScrollDeleteDelayTicks); // -> BossWindow: tSort 667
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 667);

        // Boss (monster id 756) is never actually summoned in this cluster (documented gap, see ValleyWarSystem's
        // own remarks) -- the very first BossWindow tick always resolves as "already defeated": tSort 668.
        system.Simulate(valleyZone, 1); // -> PostWinCooldown
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 668);

        system.Simulate(valleyZone, ValleyWarSchedule.PostWinCooldownTicks); // -> PreReset: tSort 669
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 669);

        Assert.Equal(ValleyWarPhase.PreReset, killRegistry.GetOrCreate(ValleyMapId).Phase);
    }

    [Fact]
    public void BossDefeated_GrantsRewardDrops_ShardWide_OnlyToLiveNonTransferringWinningTribeMembers()
    {
        const byte winningTribe = 2;

        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([ValleyMapId, OtherMapId]);

        var (valleyZone, _, _) = EnterPlayer(registry, ValleyMapId, 1, winningTribe); // on the war map itself
        var (otherZone, _, _) = EnterPlayer(registry, OtherMapId, 2, winningTribe); // elsewhere, eligible
        EnterPlayer(registry, OtherMapId, 3, winningTribe); // dead -> ineligible
        EnterPlayer(registry, OtherMapId, 4, winningTribe); // mid zone-transfer -> ineligible
        EnterPlayer(registry, OtherMapId, 5, 0); // wrong tribe -> ineligible

        Assert.True(otherZone.TryGetPlayer(3, out var deadPlayer));
        deadPlayer!.IsDead = true;
        Assert.True(otherZone.TryGetPlayer(4, out var movingPlayer));
        movingPlayer!.IsMovingZone = true;

        var (system, killRegistry) = CreateSystem(registry);
        AdvanceToKillRaceStart(system, killRegistry, valleyZone);

        killRegistry.GetOrCreate(ValleyMapId).ForceZeroTribeQuota(winningTribe);
        system.Simulate(valleyZone, 1); // -> ScrollPending
        system.Simulate(valleyZone, ValleyWarSchedule.ScrollDeleteDelayTicks); // -> BossWindow
        system.Simulate(valleyZone, 1); // -> boss defeated: posts GrantValleyWarRewardDrop to every eligible zone

        valleyZone.Tick(TimeSpan.FromMilliseconds(50)); // drain the war map's own posted command
        otherZone.Tick(TimeSpan.FromMilliseconds(50)); // drain the cross-zone posted commands

        Assert.Equal(7, valleyZone.GroundItemCount); // character 1: eligible, on the war map itself
        Assert.Equal(7, otherZone.GroundItemCount); // ONLY character 2's grant landed -- 3/4/5 excluded
    }

    [Fact]
    public void AllSessionsShouldDisconnect_OnlyDisconnectsTheValleyZonesOwnPlayers_OtherZoneUntouched()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([ValleyMapId, OtherMapId]);
        var (valleyZone, valleySession, _) = EnterPlayer(registry, ValleyMapId, 1, 0);
        var (_, otherSession, _) = EnterPlayer(registry, OtherMapId, 2, 0);

        var (system, killRegistry) = CreateSystem(registry);
        AdvanceToKillRaceStart(system, killRegistry, valleyZone);

        // Empty-map path straight to PreReset (see KillRace_MapReadsEmpty_... above).
        Assert.True(valleyZone.TryGetPlayer(1, out var solePlayer));
        solePlayer!.IsMovingZone = true;
        system.Simulate(valleyZone, 1); // -> PreReset
        Assert.Equal(ValleyWarPhase.PreReset, killRegistry.GetOrCreate(ValleyMapId).Phase);

        system.Simulate(valleyZone, ValleyWarSchedule.PreResetTicks); // -> forced disconnect, reset to Idle

        Assert.Equal(DisconnectReason.ValleyWarForcedReset, valleySession.DisconnectReason);
        Assert.Null(otherSession.DisconnectReason);
        Assert.Equal(ValleyWarPhase.Idle, killRegistry.GetOrCreate(ValleyMapId).Phase);
    }

    /// <summary>
    ///     <see cref="Monsters.MonsterSpawnScheduler" />'s own per-kill credit path reaches
    ///     <see cref="ValleyWarSchedule" /> through <see cref="ValleyWarKillRegistry.RegisterMonsterKill" />, not
    ///     the schedule directly -- this proves that wrapper decrements the SAME schedule instance
    ///     <see cref="ValleyWarSystem" /> is ticking for that map, not an independent one.
    /// </summary>
    [Fact]
    public void KillRegistry_RegisterMonsterKill_DecrementsTheSameScheduleInstanceTheSystemTicks()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([ValleyMapId, OtherMapId]);
        var (valleyZone, _, _) = EnterPlayer(registry, ValleyMapId, 1, 3);
        var (_, _, witnessPipe) = EnterPlayer(registry, OtherMapId, 2, 0);

        var (system, killRegistry) = CreateSystem(registry);
        AdvanceToKillRaceStart(system, killRegistry, valleyZone);
        ZoneTestKit.DrainOutbound(witnessPipe);

        for (var i = 0; i < ValleyWarSchedule.KillQuotaPerTribeStart; i++)
            killRegistry.RegisterMonsterKill(ValleyMapId, 3);

        system.Simulate(valleyZone, 1);

        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 666, 3);
    }
}
