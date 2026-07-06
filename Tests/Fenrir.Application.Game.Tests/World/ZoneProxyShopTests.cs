using System.Buffers.Binary;
using System.Text;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <c>Zone.RebroadcastProxyShops</c> (the offline/deputy "proxy" shop periodic in-world rebroadcast +
///     expiry force-close sweep) end-to-end through <see cref="Zone" />'s public surface --
///     <see cref="Zone.RegisterProxyShop" />/<see cref="Zone.RemoveProxyShop" />/
///     <see cref="Zone.DrainPendingProxyShopCloses" />/<see cref="Zone.ProxyShopCount" /> -- driven purely by
///     <see cref="Zone.Tick" />, no timers/sleeps.
/// </summary>
public class ZoneProxyShopTests
{
    private static readonly int OneFrame = FrameWriter.FrameSizeOf<ProxyShopStallStateResponse>();

    private static ProxyShopBroadcastEntry Entry(int characterId = 999, string ownerName = "Seller",
        string shopName = "TestStall", float posX = 10f, float posY = 0f, float posZ = 10f, int? shopDate = null)
    {
        return new ProxyShopBroadcastEntry(characterId, characterId * 2 + 1, ownerName, shopName, posX, posY, posZ,
            shopDate ?? GameDate.Today());
    }

    [Fact]
    public void RegisteredShop_IsBroadcastToNeighbor_OnceItsThrottleWindowElapses()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        // Same AOI cell as the shop (cell size 75, both floor to (0, 0)).
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(session, ProxyShopZonePolicy.ZoneNumber, posX: 20f,
            posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        zone.RegisterProxyShop(Entry());

        // No instant-visibility special case at registration (there is no cited legacy behavior for one) --
        // the shop's first keep-alive waits for the same 5 s throttle as every later one.
        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval);

        Assert.Equal(OneFrame, ZoneTestKit.DrainOutbound(pipe).Length);
        Assert.Equal(1, zone.ProxyShopCount);
    }

    [Fact]
    public void RegisteredShop_IsNotBroadcast_BeforeTheFiveSecondThrottleElapses()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(session, ProxyShopZonePolicy.ZoneNumber, posX: 20f,
            posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        zone.RegisterProxyShop(Entry());

        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval - TimeSpan.FromMilliseconds(100));
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(1, zone.ProxyShopCount);
    }

    [Fact]
    public void RegisteredShop_IsReBroadcast_OncePerShopFiveSecondThrottleElapses()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(session, ProxyShopZonePolicy.ZoneNumber, posX: 20f,
            posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        zone.RegisterProxyShop(Entry());
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval);
        Assert.Equal(OneFrame, ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public void ShopWithNoNeighbors_IsNeverBroadcastTo()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);

        zone.RegisterProxyShop(Entry());
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval + TimeSpan.FromSeconds(1));

        // No exception, no recipient -- and the shop is still tracked (still open, just unseen).
        Assert.Equal(1, zone.ProxyShopCount);
    }

    [Fact]
    public void PlayerOutsideTheShopsAoiCell_NeverReceivesTheBroadcast()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        // Far away: a different AOI cell (cell size 75) than the shop at (10, 10).
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(session, ProxyShopZonePolicy.ZoneNumber, posX: 5000f,
            posZ: 5000f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        zone.RegisterProxyShop(Entry());
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void MapThatIsNotTheProxyShopZone_NeverRunsTheSweep_EvenWithARegisteredShopAndANeighbor()
    {
        // ProxyShopZonePolicy.ZoneNumber (37) is the only map this sweep ever runs on -- see its own remarks.
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(session, 1, posX: 20f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        zone.RegisterProxyShop(Entry());
        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval + TimeSpan.FromSeconds(1));

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void PerShopThrottle_IsIndependent_NotSharedAcrossTheTable()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(session, ProxyShopZonePolicy.ZoneNumber, posX: 20f,
            posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        // Shop A registers one 50 ms tick before shop B does, so their two 5 s throttle windows (each
        // starting from its own registration moment) are offset by that same 50 ms gap in absolute
        // zone-clock terms.
        zone.RegisterProxyShop(Entry(101, "SellerA", "StallA"));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe)); // neither shop is due yet

        zone.RegisterProxyShop(Entry(102, "SellerB", "StallB"));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe)); // still neither shop is due

        // Lands strictly inside shop A's first 5 s due window (registered 50 ms before shop B's), but
        // strictly before shop B's own window opens.
        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval - TimeSpan.FromMilliseconds(90));

        // Exactly one shop's worth of traffic this tick (shop A only) -- shop B stays silent on its own clock.
        Assert.Equal(OneFrame, ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public void ReRegisteringTheSameCharacter_ReplacesThePriorEntryOutright()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);

        zone.RegisterProxyShop(Entry(999, "Seller", "OldName", 10f, posZ: 10f));
        zone.RegisterProxyShop(Entry(999, "Seller", "NewName", 99f, posZ: 99f));

        Assert.Equal(1, zone.ProxyShopCount); // never two entries for the same character
    }

    [Fact]
    public void RemoveProxyShop_StopsFurtherBroadcasts()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(session, ProxyShopZonePolicy.ZoneNumber, posX: 20f,
            posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        zone.RegisterProxyShop(Entry());
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        zone.RemoveProxyShop(999);
        Assert.Equal(0, zone.ProxyShopCount);

        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval + TimeSpan.FromSeconds(1));
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void RemoveProxyShop_OnAnUnknownCharacter_IsANoOp()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);

        zone.RemoveProxyShop(12345); // never registered -- must not throw

        Assert.Equal(0, zone.ProxyShopCount);
    }

    [Fact]
    public void ExpiredShop_IsRemovedFromTheTable_AndQueuedForItsDurableClose()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);

        zone.RegisterProxyShop(Entry(shopDate: GameDate.Today() - 1)); // expired yesterday
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, zone.ProxyShopCount);
        Assert.Contains(999, zone.DrainPendingProxyShopCloses());
    }

    [Fact]
    public void ShopExpiringToday_IsNotYetForceClosed()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);

        zone.RegisterProxyShop(Entry(shopDate: GameDate.Today())); // expires strictly before today, not on it
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, zone.ProxyShopCount);
        Assert.Empty(zone.DrainPendingProxyShopCloses());
    }

    [Fact]
    public void DrainPendingProxyShopCloses_ReturnsEachQueuedCloseExactlyOnce()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);

        zone.RegisterProxyShop(Entry(101, shopDate: GameDate.Today() - 1));
        zone.RegisterProxyShop(Entry(102, shopDate: GameDate.Today() - 1));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var drained = zone.DrainPendingProxyShopCloses();
        Assert.Equal([101, 102], drained.OrderBy(x => x));
        Assert.Empty(zone.DrainPendingProxyShopCloses()); // drained exactly once
    }

    [Fact]
    public void TryUpdateProxyShopExpiration_ExtendsAnAlreadyRegisteredShop_WithoutDiscardingItsBroadcastEntry()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);

        zone.RegisterProxyShop(Entry(shopDate: GameDate.Today() - 1)); // would otherwise expire on the next sweep
        Assert.True(zone.TryUpdateProxyShopExpiration(999, GameDate.Today() + 30));

        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, zone.ProxyShopCount); // survived the sweep
        Assert.Empty(zone.DrainPendingProxyShopCloses());
    }

    [Fact]
    public void TryUpdateProxyShopExpiration_OnAnUnregisteredCharacter_ReturnsFalse()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);

        Assert.False(zone.TryUpdateProxyShopExpiration(54321, GameDate.Today() + 30));
    }

    [Fact]
    public void BroadcastFrame_CarriesTheShopsIdentityLocationAndNames()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(session, ProxyShopZonePolicy.ZoneNumber, posX: 20f,
            posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        zone.RegisterProxyShop(Entry(777, "Vidar", "VidarStall", 11f, 2f, 13f));
        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame, frame.Length);

        // 1-byte opcode header, then the payload: ServerIndex, UniqueNumber, ProxyStateInfo, 2-byte pad,
        // CheckChangeActionState -- see ProxyShopStallStateResponse/ProxyStateInfo's own wire layout.
        var payload = frame.AsSpan(1);
        Assert.Equal(777, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(777 * 2 + 1, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        Assert.Equal(11f, BinaryPrimitives.ReadSingleLittleEndian(payload[8..]));
        Assert.Equal(2f, BinaryPrimitives.ReadSingleLittleEndian(payload[12..]));
        Assert.Equal(13f, BinaryPrimitives.ReadSingleLittleEndian(payload[16..]));
        Assert.Equal("Vidar", Encoding.Latin1.GetString(payload.Slice(20, 13)).TrimEnd('\0'));
        Assert.Equal("VidarStall", Encoding.Latin1.GetString(payload.Slice(33, 25)).TrimEnd('\0'));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[(20 + 13 + 25 + 2)..]));
    }
}
