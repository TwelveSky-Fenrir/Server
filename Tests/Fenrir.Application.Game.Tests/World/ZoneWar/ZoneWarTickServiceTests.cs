using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Hosting.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class ZoneWarTickServiceTests
{
    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options),
            new MovementRules(Options.Create(options)), new DirtyTracker<int>(), NullLogger<Zone>.Instance,
            ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize(maps);
        return registry;
    }

    [Fact]
    public void NoActiveWar_TickNeverBroadcastsAnything()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var service = new ZoneWarTickService(registry);

        service.Tick(SimulationClock.LegacyTick * 20);

        Assert.False(service.IsActive);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Zone049_TribeUserNum_IsAlwaysTheLiveHeadcount_AcrossEveryZone_IgnoringReportedStandings()
    {
        var registry = CreateRegistry(1, 2);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);
        var (sessionC, pipeC) = ZoneTestKit.CreateSession(3);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1, tribe: 0)));
        registry[1].Post(ZoneCommand.Enter(11, ZoneTestKit.EnterData(sessionB, 1, tribe: 0)));
        registry[2].Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionC, 2, tribe: 2)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        registry[2].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);
        ZoneTestKit.DrainOutbound(pipeC);

        var service = new ZoneWarTickService(registry);
        service.StartWar(ZoneWarKind.Zone049, 30);
        service.ReportStanding(0, 999); // must be ignored -- Zone049 always computes this live

        service.Tick(SimulationClock.LegacyTick);

        var payload = ZoneTestKit.DrainOutbound(pipeA).AsSpan(1);
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(payload)); // tribe 0
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[4..])); // tribe 1
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(payload[8..])); // tribe 2
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[12..])); // tribe 3
        Assert.Equal(30, BinaryPrimitives.ReadInt32LittleEndian(payload[16..]));
    }

    [Fact]
    public void Zone051_BroadcastsReportedExistStone_NotLiveHeadcount()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, tribe: 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var service = new ZoneWarTickService(registry);
        service.StartWar(ZoneWarKind.Zone051, 12);
        service.ReportStanding(3, 1);

        service.Tick(SimulationClock.LegacyTick);

        var payload = ZoneTestKit.DrainOutbound(pipe).AsSpan(1);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[8..]));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(payload[12..]));
        Assert.Equal(12, BinaryPrimitives.ReadInt32LittleEndian(payload[16..]));
    }

    [Fact]
    public void Cadence_BroadcastsOnTick1_ThenEveryTenthTick_DecrementingOncePerSecond()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var service = new ZoneWarTickService(registry);
        service.StartWar(ZoneWarKind.Zone241, 10);

        for (var i = 0; i < 9; i++)
        {
            service.Tick(SimulationClock.LegacyTick);
            if (i == 0)
                Assert.Equal(10, BinaryPrimitives.ReadInt32LittleEndian(ZoneTestKit.DrainOutbound(pipe).AsSpan(1)));
            else
                Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
        }

        service.Tick(SimulationClock.LegacyTick); // 10th tick: due again
        Assert.Equal(5, BinaryPrimitives.ReadInt32LittleEndian(ZoneTestKit.DrainOutbound(pipe).AsSpan(1)));
        Assert.Equal(5, service.RemainTime);
    }

    [Fact]
    public void War_EndsAutomatically_WhenRemainTimeReachesZero_AndStopsBroadcastingAfter()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var service = new ZoneWarTickService(registry);
        service.StartWar(ZoneWarKind.Zone335, 1);

        service.Tick(SimulationClock.LegacyTick); // tick 1: broadcast RemainTime=1, no decrement yet
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(ZoneTestKit.DrainOutbound(pipe).AsSpan(1)));
        Assert.True(service.IsActive);

        service.Tick(SimulationClock.LegacyTick); // tick 2: decrements to 0 -> ends, still broadcasts the 0
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(ZoneTestKit.DrainOutbound(pipe).AsSpan(1)));
        Assert.False(service.IsActive);
        Assert.Null(service.ActiveKind);

        service.Tick(SimulationClock.LegacyTick * 20);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Zone194_BroadcastsBothTheCountdownAndTheFullStatusFrame_OnEveryCadenceTick()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var service = new ZoneWarTickService(registry);
        service.StartWar(ZoneWarKind.Zone194, 20);
        service.ReportStanding(1, 7);

        service.Tick(SimulationClock.LegacyTick);

        var bytes = ZoneTestKit.DrainOutbound(pipe);
        var countdownFrameSize = FrameWriter.FrameSizeOf<ZoneWar194CountdownResponse>();
        var statusFrameSize = FrameWriter.FrameSizeOf<ZoneWar194StatusResponse>();
        Assert.Equal(countdownFrameSize + statusFrameSize, bytes.Length);

        Assert.Equal(20, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(1)));

        var statusPayload = bytes.AsSpan(countdownFrameSize + 1);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(statusPayload));
        Assert.Equal(7, BinaryPrimitives.ReadInt32LittleEndian(statusPayload[4..]));
        Assert.Equal(20, BinaryPrimitives.ReadInt32LittleEndian(statusPayload[16..]));
    }

    [Fact]
    public void Zone297_BroadcastsExtraStatusAndMonsterCount_AsTwoSeparateFrames()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var service = new ZoneWarTickService(registry);
        service.StartWar(ZoneWarKind.Zone297, 42);
        service.ReportExtraStatus(3, 4);
        service.ReportMonsterCount(2, 9);

        service.Tick(SimulationClock.LegacyTick);

        var bytes = ZoneTestKit.DrainOutbound(pipe);
        var statusFrameSize = FrameWriter.FrameSizeOf<ZoneWar297StatusResponse>();
        var monsterFrameSize = FrameWriter.FrameSizeOf<ZoneWar297MonsterCountResponse>();
        Assert.Equal(statusFrameSize + monsterFrameSize, bytes.Length);

        var statusPayload = bytes.AsSpan(1);
        Assert.Equal(42, BinaryPrimitives.ReadInt32LittleEndian(statusPayload));
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(statusPayload[4..]));
        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(statusPayload[8..]));

        var monsterPayload = bytes.AsSpan(statusFrameSize + 1);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(monsterPayload));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(monsterPayload[4..]));
        Assert.Equal(9, BinaryPrimitives.ReadInt32LittleEndian(monsterPayload[8..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(monsterPayload[12..]));
    }

    [Fact]
    public void StartWar_ResetsEveryPreviouslyReportedValue()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var service = new ZoneWarTickService(registry);
        service.StartWar(ZoneWarKind.Zone267, 10);
        service.ReportStanding(0, 55);
        service.EndWar();

        service.StartWar(ZoneWarKind.Zone267, 8);
        service.Tick(SimulationClock.LegacyTick);

        var payload = ZoneTestKit.DrainOutbound(pipe).AsSpan(1);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(8, BinaryPrimitives.ReadInt32LittleEndian(payload[16..]));
    }

    [Fact]
    public void ReportStanding_OutOfRangeTribeId_Throws()
    {
        var registry = CreateRegistry(1);
        var service = new ZoneWarTickService(registry);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.ReportStanding(4, 1));
    }
}
