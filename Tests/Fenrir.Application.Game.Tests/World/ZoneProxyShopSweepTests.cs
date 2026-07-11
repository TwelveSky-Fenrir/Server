using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneProxyShopSweepTests
{
    private const int NotExpired = 20991231;
    private const int Expired = 20200101;
    private static readonly int ActionStateOffset = 1 + 4 + 4 + ProxyStateInfo.WireSize + 2;
    private static readonly int OneFrame = FrameWriter.FrameSizeOf<ProxyShopStallStateResponse>();

    private static ProxyShopBroadcastEntry Entry(int characterId, int shopDate, float x = 10f, float z = 10f)
    {
        return new ProxyShopBroadcastEntry(characterId, characterId * 2 + 1, "Owner", "Shop", x, 0f, z, shopDate);
    }

    private static int ReadActionState(byte[] frame)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(ActionStateOffset));
    }

    private static (Zone Zone, FakeDuplexPipe NeighborPipe) SetUpZoneWithNeighbor()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(session, ProxyShopZonePolicy.ZoneNumber, posX: 10f,
            posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        return (zone, pipe);
    }

    [Fact]
    public void RegisteredShop_NotYetDue_NotExpired_NoBroadcastBeforeTheThrottleElapses()
    {
        var (zone, pipe) = SetUpZoneWithNeighbor();
        zone.RegisterProxyShop(Entry(10, NotExpired));

        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval - TimeSpan.FromMilliseconds(100));

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(1, zone.ProxyShopCount);
    }

    [Fact]
    public void RegisteredShop_NotExpired_SendsThePeriodicRefreshBroadcast_AfterTheThrottleElapses()
    {
        var (zone, pipe) = SetUpZoneWithNeighbor();
        zone.RegisterProxyShop(Entry(10, NotExpired));

        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame, frame.Length);
        Assert.Equal(0, ReadActionState(frame));
        Assert.Equal(1, zone.ProxyShopCount);
    }

    [Fact]
    public void RegisteredShop_Expired_BroadcastsTheDistinctRemovalActionState_AndDropsTheEntry()
    {
        var (zone, pipe) = SetUpZoneWithNeighbor();
        zone.RegisterProxyShop(Entry(10, Expired));

        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame, frame.Length);
        Assert.Equal(3, ReadActionState(frame));
        Assert.Equal(0, zone.ProxyShopCount);
    }

    [Fact]
    public void RegisteredShop_Expired_QueuesTheDurableCloseWrite_OnlyOncePerExpiry()
    {
        var (zone, _) = SetUpZoneWithNeighbor();
        zone.RegisterProxyShop(Entry(10, Expired));

        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval);

        var pending = zone.DrainPendingProxyShopCloses();
        Assert.Single(pending);
        Assert.Equal(10, pending[0]);

        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval);
        Assert.Empty(zone.DrainPendingProxyShopCloses());
    }

    [Fact]
    public void ExpiryExactlyToday_IsNotForceClosed_StrictLessThanOnly()
    {
        var (zone, pipe) = SetUpZoneWithNeighbor();
        zone.RegisterProxyShop(Entry(10, GameDate.Today()));

        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame, frame.Length);
        Assert.Equal(0, ReadActionState(frame));
        Assert.Equal(1, zone.ProxyShopCount);
    }

    [Fact]
    public void WrongZone_SweepNeverRuns_EvenForAnAlreadyExpiredEntry()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber + 1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1,
            ZoneTestKit.EnterData(session, ProxyShopZonePolicy.ZoneNumber + 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        zone.RegisterProxyShop(Entry(10, Expired));
        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(1, zone.ProxyShopCount);
        Assert.Empty(zone.DrainPendingProxyShopCloses());
    }

    [Fact]
    public void NoNeighborsNearby_StillForceClosesAndQueuesTheWrite_JustSendsNoPacket()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        zone.RegisterProxyShop(Entry(10, Expired, 5000f, 5000f));

        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval);

        Assert.Equal(0, zone.ProxyShopCount);
        Assert.Single(zone.DrainPendingProxyShopCloses());
    }
}
