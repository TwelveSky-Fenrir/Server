using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Handlers.Handlers.Commerce;
using Fenrir.Application.Game.Services.Commerce;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Commerce;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

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

    private static SearchShopListingsService CreateService(FakeOfflineShopRepository? offlineShops = null)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            // Sort 11 -> EPSORT_RING (category 6).
            [500] = new(WorldDataTestRows.Item(500) with { Sort = 11 }, []),
            // Sort 50 isn't mapped by SortToCategory's ~10 named sorts at all -- a known/cataloged item the
            // legacy's own default-inclusion rule (S04_MyWork02.cpp:6681-6909) still surfaces under an
            // unfiltered "show all" query.
            [600] = new(WorldDataTestRows.Item(600) with { Sort = 50 }, [])
        }.ToFrozenDictionary();

        return new SearchShopListingsService(ZoneTestKit.EmptyWorldData(itemsById),
            offlineShops ?? new FakeOfflineShopRepository(), NullLogger<SearchShopListingsService>.Instance);
    }

    [Fact]
    public async Task WrongZone_NoReply()
    {
        var zone = ZoneTestKit.CreateZone(1);
        Setup(zone, 10);
        var service = CreateService();

        var results = await service.SearchAsync(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 0 }, zone,
            CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task ShowAll_MatchesCatalogedSort_SendsOneListing()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        Setup(zone, 10);
        Setup(zone, 20, "Seller");

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopOpen = true;
        seller.PshopListing = WithSlot(40, 1, 2, 500, 3, 0, 7, 999);

        var service = CreateService();
        var results = await service.SearchAsync(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 0 }, zone,
            CancellationToken.None);

        Assert.Single(results);
    }

    [Fact]
    public async Task ShowAll_SellerNotOpen_NoListings()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        Setup(zone, 10);
        Setup(zone, 20, "Seller");

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopListing = WithSlot(40, 1, 2, 500, 3, 0, 7, 999);
        // PshopOpen deliberately left false.

        var service = CreateService();
        var results = await service.SearchAsync(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 0 }, zone,
            CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task UnknownItemId_ExcludedEvenFromShowAll()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        Setup(zone, 10);
        Setup(zone, 20, "Seller");

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopOpen = true;
        // Item 999 isn't in the handler's item catalog at all -- unlike a cataloged item with an
        // uncategorized sort (see CatalogedItemWithUncategorizedSort_* below), there is nothing here for
        // even the legacy default-inclusion rule to classify.
        seller.PshopListing = WithSlot(40, 0, 0, 999, 1, 0, 1, 100);

        var service = CreateService();
        var results = await service.SearchAsync(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 0 }, zone,
            CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task CatalogedItemWithUncategorizedSort_ShowAll_DefaultBranchIncludesIt()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        Setup(zone, 10);
        Setup(zone, 20, "Seller");

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopOpen = true;
        // Item 600 IS cataloged but its sort (50) isn't one of SortToCategory's ~10 mapped sorts --
        // S04_MyWork02.cpp:6681-6909's own default-inclusion rule still surfaces it under an unfiltered
        // "show all" (type=ALL, category=ALL) query; only an explicit non-ALL category filter genuinely
        // excludes it (see the next test).
        seller.PshopListing = WithSlot(40, 0, 0, 600, 1, 0, 1, 100);

        var service = CreateService();
        var results = await service.SearchAsync(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 0 }, zone,
            CancellationToken.None);

        Assert.Single(results);
    }

    [Fact]
    public async Task CatalogedItemWithUncategorizedSort_ExplicitCategoryFilter_Excluded()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        Setup(zone, 10);
        Setup(zone, 20, "Seller");

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopOpen = true;
        seller.PshopListing = WithSlot(40, 0, 0, 600, 1, 0, 1, 100);

        var service = CreateService();
        // Sort1=ALL, Sort2=6 (Ring) -- an explicit, non-ALL category filter genuinely excludes an item
        // neither classification pass can resolve.
        var results = await service.SearchAsync(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 6 }, zone,
            CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task CategoryFilter_MismatchedSort2_Excluded()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        Setup(zone, 10);
        Setup(zone, 20, "Seller");

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopOpen = true;
        seller.PshopListing = WithSlot(40, 0, 0, 500, 1, 0, 1, 100); // category 6 (Ring)

        var service = CreateService();
        // Sort1=ALL, Sort2=1 (Skill) -- doesn't match the ring's category (6).
        var results = await service.SearchAsync(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 1 }, zone,
            CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task CategoryFilter_MatchingSort2_Included()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        Setup(zone, 10);
        Setup(zone, 20, "Seller");

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopOpen = true;
        seller.PshopListing = WithSlot(40, 0, 0, 500, 1, 0, 1, 100); // category 6 (Ring)

        var service = CreateService();
        var results = await service.SearchAsync(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 6 }, zone,
            CancellationToken.None);

        Assert.Single(results);
    }

    [Fact]
    public async Task ProxyShopListing_MatchingFilter_Included()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        Setup(zone, 10);

        var offlineShops = new FakeOfflineShopRepository();
        // SlotIndex 7 = page 1, slot 2 (5 slots/page) -- category 6 (Ring, item 500).
        offlineShops.SeedOpenListings(new OfflineShopOpenListingRowDto(99, "Deputy", 7, 500, 2, 0, 5, 250, null));

        var service = CreateService(offlineShops);
        var results = await service.SearchAsync(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 0 }, zone,
            CancellationToken.None);

        var entry = Assert.Single(results);
        Assert.Equal("Deputy", entry.AvatarName);
        Assert.Equal(unchecked((uint)(99 * 2 + 1)), entry.UniqueNumber);
        Assert.Equal(1, entry.Page);
        Assert.Equal(2, entry.Index);
        Assert.Equal(500, entry.PshopItemInfo[0]);
        Assert.Equal(2, entry.PshopItemInfo[1]);
        Assert.Equal(250, entry.PshopItemInfo[4]);
    }

    [Fact]
    public async Task ProxyShopListing_UnionedWithLivePersonalShop_BothIncluded()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        Setup(zone, 10);
        Setup(zone, 20, "Seller");

        Assert.True(zone.TryGetPlayer(20, out var seller));
        seller!.PshopOpen = true;
        seller.PshopListing = WithSlot(40, 0, 0, 500, 1, 0, 1, 100);

        var offlineShops = new FakeOfflineShopRepository();
        offlineShops.SeedOpenListings(new OfflineShopOpenListingRowDto(99, "Deputy", 0, 500, 1, 0, 1, 100, null));

        var service = CreateService(offlineShops);
        var results = await service.SearchAsync(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 0 }, zone,
            CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.AvatarName == "Deputy");
        Assert.Contains(results, r => r.AvatarName == "Seller");
    }

    [Fact]
    public async Task ProxyShopListing_CategoryFilterMismatch_Excluded()
    {
        var zone = ZoneTestKit.CreateZone(OpenShopStallHandler.PshopZoneNumber);
        Setup(zone, 10);

        var offlineShops = new FakeOfflineShopRepository();
        offlineShops.SeedOpenListings(
            new OfflineShopOpenListingRowDto(99, "Deputy", 0, 500, 1, 0, 1, 100, null)); // category 6 (Ring)

        var service = CreateService(offlineShops);
        // Sort1=ALL, Sort2=1 (Skill) -- doesn't match the ring's category (6).
        var results = await service.SearchAsync(new SearchShopListingsRequest { Sort1 = 0, Sort2 = 1 }, zone,
            CancellationToken.None);

        Assert.Empty(results);
    }
}
