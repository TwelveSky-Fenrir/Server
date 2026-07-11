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

public class ValleyWarSystemTests
{
    private const short ValleyMapId = 200;
    private const short OtherMapId = 1;

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

        system.Simulate(valleyZone, ValleyWarSchedule.IdleWaitTicks + ValleyWarSchedule.GateCountdownIntervalTicks);
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(valleyPipe), 659, 5);
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 659, 5);

        system.Simulate(valleyZone, 4 * ValleyWarSchedule.GateCountdownIntervalTicks);
        ZoneTestKit.DrainOutbound(valleyPipe);
        ZoneTestKit.DrainOutbound(witnessPipe);

        system.Simulate(valleyZone, ValleyWarSchedule.GateCountdownIntervalTicks);
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(valleyPipe), 660);
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 660);

        system.Simulate(valleyZone, ValleyWarSchedule.GateOpenTicks);
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

        system.Simulate(valleyZone,
            ValleyWarSchedule.IdleWaitTicks +
            (ValleyWarSchedule.GateCountdownStartValue + 1) * ValleyWarSchedule.GateCountdownIntervalTicks +
            ValleyWarSchedule.GateOpenTicks);
        ZoneTestKit.DrainOutbound(valleyPipe);
        ZoneTestKit.DrainOutbound(witnessPipe);

        system.Simulate(valleyZone, ValleyWarSchedule.DoorPendingTicks - 1);
        var earlyDoorFrames = ZoneTestKit.DrainOutbound(valleyPipe);
        Assert.Equal(9 * FrameWriter.FrameSizeOf<ZoneWar297StatusResponse>(), earlyDoorFrames.Length);
        Assert.Empty(ZoneTestKit.DrainOutbound(witnessPipe));

        system.Simulate(valleyZone, 1);
        Assert.Equal(ValleyWarPhase.KillRace, killRegistry.GetOrCreate(ValleyMapId).Phase);

        var finalValleyFrames = ZoneTestKit.DrainOutbound(valleyPipe);
        Assert.Equal(
            FrameWriter.FrameSizeOf<ZoneWar297StatusResponse>() + FrameWriter.FrameSizeOf<ZoneEventInfoResponse>(),
            finalValleyFrames.Length);
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 663);

        system.Simulate(valleyZone, 2);
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

        Assert.True(valleyZone.TryGetPlayer(1, out var solePlayer));
        solePlayer!.IsMovingZone = true;

        system.Simulate(valleyZone, 1);

        Assert.Equal(ValleyWarPhase.PreReset, killRegistry.GetOrCreate(ValleyMapId).Phase);
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 669);
        Assert.Null(valleySession
            .DisconnectReason);
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

        system.Simulate(valleyZone, 1);
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 666, 2);

        system.Simulate(valleyZone, ValleyWarSchedule.ScrollDeleteDelayTicks);
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 667);

        system.Simulate(valleyZone, 1);
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 668);

        system.Simulate(valleyZone, ValleyWarSchedule.PostWinCooldownTicks);
        AssertZoneEventInfo(ZoneTestKit.DrainOutbound(witnessPipe), 669);

        Assert.Equal(ValleyWarPhase.PreReset, killRegistry.GetOrCreate(ValleyMapId).Phase);
    }

    [Fact]
    public void BossDefeated_GrantsRewardDrops_ShardWide_OnlyToLiveNonTransferringWinningTribeMembers()
    {
        const byte winningTribe = 2;

        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([ValleyMapId, OtherMapId]);

        var (valleyZone, _, _) = EnterPlayer(registry, ValleyMapId, 1, winningTribe);
        var (otherZone, _, _) = EnterPlayer(registry, OtherMapId, 2, winningTribe);
        EnterPlayer(registry, OtherMapId, 3, winningTribe);
        EnterPlayer(registry, OtherMapId, 4, winningTribe);
        EnterPlayer(registry, OtherMapId, 5, 0);

        Assert.True(otherZone.TryGetPlayer(3, out var deadPlayer));
        deadPlayer!.IsDead = true;
        Assert.True(otherZone.TryGetPlayer(4, out var movingPlayer));
        movingPlayer!.IsMovingZone = true;

        var (system, killRegistry) = CreateSystem(registry);
        AdvanceToKillRaceStart(system, killRegistry, valleyZone);

        killRegistry.GetOrCreate(ValleyMapId).ForceZeroTribeQuota(winningTribe);
        system.Simulate(valleyZone, 1);
        system.Simulate(valleyZone, ValleyWarSchedule.ScrollDeleteDelayTicks);
        system.Simulate(valleyZone, 1);

        valleyZone.Tick(TimeSpan.FromMilliseconds(50));
        otherZone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(7, valleyZone.GroundItemCount);
        Assert.Equal(7, otherZone.GroundItemCount);
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

        Assert.True(valleyZone.TryGetPlayer(1, out var solePlayer));
        solePlayer!.IsMovingZone = true;
        system.Simulate(valleyZone, 1);
        Assert.Equal(ValleyWarPhase.PreReset, killRegistry.GetOrCreate(ValleyMapId).Phase);

        system.Simulate(valleyZone, ValleyWarSchedule.PreResetTicks);

        Assert.Equal(DisconnectReason.ValleyWarForcedReset, valleySession.DisconnectReason);
        Assert.Null(otherSession.DisconnectReason);
        Assert.Equal(ValleyWarPhase.Idle, killRegistry.GetOrCreate(ValleyMapId).Phase);
    }

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
