using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class DailyResetBroadcasterTests
{
    private static int OneFrame => FrameWriter.FrameSizeOf<ZoneEventInfoResponse>();

    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize(maps);
        return registry;
    }

    [Fact]
    public void Tick_AtTheDueMinute_BroadcastsAZeroFilledSort1600Frame_ToEveryConnectedPlayer()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var broadcaster = new DailyResetBroadcaster(registry, NullLogger<DailyResetBroadcaster>.Instance);

        broadcaster.Tick(new DateTime(2026, 7, 10, 0, 1, 0, DateTimeKind.Utc));

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame, frame.Length);

        var payload = frame.AsSpan(1);
        var sort = BinaryPrimitives.ReadInt32LittleEndian(payload);
        Assert.Equal(ScheduledZoneCenterEventCodes.DailyResetEventCode, sort);

        var data = payload[4..].ToArray();
        Assert.All(data, b => Assert.Equal((byte)0, b));
    }

    [Fact]
    public void Tick_OutsideTheDueMinute_BroadcastsNothing()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var broadcaster = new DailyResetBroadcaster(registry, NullLogger<DailyResetBroadcaster>.Instance);

        broadcaster.Tick(new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc));

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Tick_AtTheDueMinuteTwice_OnlyBroadcastsOnce()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var broadcaster = new DailyResetBroadcaster(registry, NullLogger<DailyResetBroadcaster>.Instance);

        broadcaster.Tick(new DateTime(2026, 7, 10, 0, 1, 0, DateTimeKind.Utc));
        ZoneTestKit.DrainOutbound(pipe);

        broadcaster.Tick(new DateTime(2026, 7, 10, 0, 1, 30, DateTimeKind.Utc));

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }
}
