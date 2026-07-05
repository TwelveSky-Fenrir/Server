using System.Collections.Frozen;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Handlers.Commerce;
using Fenrir.Application.Game.Social.Pshop;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Framing;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Tests.Handlers.Commerce;

public class SearchShopListingsHandlerTests
{
    private static readonly int ListingFrame = FrameWriter.FrameSizeOf<SearchShopListingsResponse>();

    private static PshopInfo EmptyListing(uint uniqueNumber)
    {
        return new PshopInfo
            { UniqueNumber = uniqueNumber, Name = "Shop", ItemInfo = new int[225], SocketInfo = new int[75] };
    }

    private static PshopInfo WithSlot(uint uniqueNumber, int page, int slot, int itemId, int quantity, int value,
        int serial, int price)
    {
        var info = EmptyListing(uniqueNumber);
        var i = PshopPurchasePolicy.FlatIndex(page, slot);
        info.ItemInfo[i + 0] = itemId;
        info.ItemInfo[i + 1] = quantity;
        info.ItemInfo[i + 2] = value;
        info.ItemInfo[i + 3] = serial;
        info.ItemInfo[i + 4] = price;
        return info;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe) Setup(Zone zone, int characterId,
        string name = "Buyer")
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, zone.MapId, name)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;
        return (session, pipe);
    }

    private static SearchShopListingsHandler CreateHandler()
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            // Sort 11 -> EPSORT_RING (category 6).
            [500] = new(WorldDataTestRows.Item(500) with { Sort = 11 }, [])
        }.ToFrozenDictionary();

        return new SearchShopListingsHandler(ZoneTestKit.EmptyWorldData(itemsById));
    }

    [Fact]
    public void WrongZone_NoReply()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = Setup(zone, 10);
        var handler = CreateHandler();

        handler.Handle(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 0 }, session);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void ShowAll_MatchesCatalogedSort_SendsOneListing()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        var (session, pipe) = Setup(zone, 10);
        Setup(zone, 20, "Seller");
        ZoneTestKit.DrainOutbound(pipe); // seller's own Enter-broadcast join packet, not under test

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopOpen = true;
        seller.PshopListing = WithSlot(40, 1, 2, 500, 3, 0, 7, 999);

        var handler = CreateHandler();
        handler.Handle(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 0 }, session);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(ListingFrame, frame.Length);
    }

    [Fact]
    public void ShowAll_SellerNotOpen_NoListings()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        var (session, pipe) = Setup(zone, 10);
        Setup(zone, 20, "Seller");
        ZoneTestKit.DrainOutbound(pipe); // seller's own Enter-broadcast join packet, not under test

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopListing = WithSlot(40, 1, 2, 500, 3, 0, 7, 999);
        // PshopOpen deliberately left false.

        var handler = CreateHandler();
        handler.Handle(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 0 }, session);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void UncatalogedItemSort_ExcludedEvenFromShowAll()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        var (session, pipe) = Setup(zone, 10);
        Setup(zone, 20, "Seller");
        ZoneTestKit.DrainOutbound(pipe); // seller's own Enter-broadcast join packet, not under test

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopOpen = true;
        // Item 999 isn't in the handler's item catalog at all.
        seller.PshopListing = WithSlot(40, 0, 0, 999, 1, 0, 1, 100);

        var handler = CreateHandler();
        handler.Handle(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 0 }, session);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void CategoryFilter_MismatchedSort2_Excluded()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        var (session, pipe) = Setup(zone, 10);
        Setup(zone, 20, "Seller");
        ZoneTestKit.DrainOutbound(pipe); // seller's own Enter-broadcast join packet, not under test

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopOpen = true;
        seller.PshopListing = WithSlot(40, 0, 0, 500, 1, 0, 1, 100); // category 6 (Ring)

        var handler = CreateHandler();
        // Sort1=ALL, Sort2=1 (Skill) -- doesn't match the ring's category (6).
        handler.Handle(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 1 }, session);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void CategoryFilter_MatchingSort2_Included()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        var (session, pipe) = Setup(zone, 10);
        Setup(zone, 20, "Seller");
        ZoneTestKit.DrainOutbound(pipe); // seller's own Enter-broadcast join packet, not under test

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopOpen = true;
        seller.PshopListing = WithSlot(40, 0, 0, 500, 1, 0, 1, 100); // category 6 (Ring)

        var handler = CreateHandler();
        handler.Handle(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 6 }, session);

        Assert.Equal(ListingFrame, ZoneTestKit.DrainOutbound(pipe).Length);
    }
}
