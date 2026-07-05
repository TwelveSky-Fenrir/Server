using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Handlers.Handlers.Commerce;
using Fenrir.Application.Game.Services.Commerce;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Handlers.Commerce;

public class SearchShopListingsServiceTests
{
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

    private static SearchShopListingsService CreateService()
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            // Sort 11 -> EPSORT_RING (category 6).
            [500] = new(WorldDataTestRows.Item(500) with { Sort = 11 }, [])
        }.ToFrozenDictionary();

        return new SearchShopListingsService(ZoneTestKit.EmptyWorldData(itemsById));
    }

    [Fact]
    public void WrongZone_NoReply()
    {
        var zone = ZoneTestKit.CreateZone(1);
        Setup(zone, 10);
        var service = CreateService();

        var results = service.Search(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 0 }, zone);

        Assert.Empty(results);
    }

    [Fact]
    public void ShowAll_MatchesCatalogedSort_SendsOneListing()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        Setup(zone, 10);
        Setup(zone, 20, "Seller");

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopOpen = true;
        seller.PshopListing = WithSlot(40, 1, 2, 500, 3, 0, 7, 999);

        var service = CreateService();
        var results = service.Search(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 0 }, zone);

        Assert.Single(results);
    }

    [Fact]
    public void ShowAll_SellerNotOpen_NoListings()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        Setup(zone, 10);
        Setup(zone, 20, "Seller");

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopListing = WithSlot(40, 1, 2, 500, 3, 0, 7, 999);
        // PshopOpen deliberately left false.

        var service = CreateService();
        var results = service.Search(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 0 }, zone);

        Assert.Empty(results);
    }

    [Fact]
    public void UncatalogedItemSort_ExcludedEvenFromShowAll()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        Setup(zone, 10);
        Setup(zone, 20, "Seller");

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopOpen = true;
        // Item 999 isn't in the handler's item catalog at all.
        seller.PshopListing = WithSlot(40, 0, 0, 999, 1, 0, 1, 100);

        var service = CreateService();
        var results = service.Search(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 0 }, zone);

        Assert.Empty(results);
    }

    [Fact]
    public void CategoryFilter_MismatchedSort2_Excluded()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        Setup(zone, 10);
        Setup(zone, 20, "Seller");

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopOpen = true;
        seller.PshopListing = WithSlot(40, 0, 0, 500, 1, 0, 1, 100); // category 6 (Ring)

        var service = CreateService();
        // Sort1=ALL, Sort2=1 (Skill) -- doesn't match the ring's category (6).
        var results = service.Search(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 1 }, zone);

        Assert.Empty(results);
    }

    [Fact]
    public void CategoryFilter_MatchingSort2_Included()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        Setup(zone, 10);
        Setup(zone, 20, "Seller");

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopOpen = true;
        seller.PshopListing = WithSlot(40, 0, 0, 500, 1, 0, 1, 100); // category 6 (Ring)

        var service = CreateService();
        var results = service.Search(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 6 }, zone);

        Assert.Single(results);
    }
}
