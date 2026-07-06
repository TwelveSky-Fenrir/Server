using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     <see cref="Zone.Tick" />'s proxy/deputy-shop periodic sweep (<c>Zone.RebroadcastProxyShops</c>,
///     Server/ts25zone/S07_MyGame01.cpp:2586-2608): per-shop 5 s throttle, strict-less-than expiry
///     force-close (broadcasting the distinct "gone" action state before queuing the durable close), and the
///     periodic "still here" refresh broadcast otherwise. <see cref="World.ZoneProxyShopsTests" /> already
///     covers <see cref="Zone.TryUpdateProxyShopExpiration" /> in isolation; this file covers the sweep's own
///     broadcast/throttle/expiry behavior end to end.
/// </summary>
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
        // same AOI cell as the shop entries below (cell size 75, both floor to (0, 0))
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(session, ProxyShopZonePolicy.ZoneNumber, posX: 10f,
            posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe); // discard the entry handshake noise
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
        Assert.Equal(0, ReadActionState(frame)); // periodic re-broadcast, not a despawn
        Assert.Equal(1, zone.ProxyShopCount); // still tracked -- not force-closed
    }

    [Fact]
    public void RegisteredShop_Expired_BroadcastsTheDistinctRemovalActionState_AndDropsTheEntry()
    {
        var (zone, pipe) = SetUpZoneWithNeighbor();
        zone.RegisterProxyShop(Entry(10, Expired));

        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame, frame.Length);
        Assert.Equal(3, ReadActionState(frame)); // distinct "stall gone" code, not the refresh code
        Assert.Equal(0, zone.ProxyShopCount); // force-closed -- no longer tracked
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

        // Already removed from the broadcast table -- a later tick must not enqueue it again.
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
        Assert.Equal(0, ReadActionState(frame)); // still the refresh code -- expiring "today" isn't past-due yet
        Assert.Equal(1, zone.ProxyShopCount);
    }

    [Fact]
    public void WrongZone_SweepNeverRuns_EvenForAnAlreadyExpiredEntry()
    {
        // Deliberately NOT ProxyShopZonePolicy.ZoneNumber.
        var zone = ZoneTestKit.CreateZone((short)(ProxyShopZonePolicy.ZoneNumber + 1));
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1,
            ZoneTestKit.EnterData(session, (short)(ProxyShopZonePolicy.ZoneNumber + 1), posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        zone.RegisterProxyShop(Entry(10, Expired));
        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(1, zone.ProxyShopCount); // never even evaluated, let alone force-closed
        Assert.Empty(zone.DrainPendingProxyShopCloses());
    }

    [Fact]
    public void NoNeighborsNearby_StillForceClosesAndQueuesTheWrite_JustSendsNoPacket()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        zone.RegisterProxyShop(Entry(10, Expired, x: 5000f, z: 5000f)); // far outside any AOI neighbor

        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval);

        Assert.Equal(0, zone.ProxyShopCount);
        Assert.Single(zone.DrainPendingProxyShopCloses());
    }
}
