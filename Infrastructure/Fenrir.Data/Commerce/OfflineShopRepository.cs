using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;

namespace Fenrir.Data.Commerce;

public sealed record OfflineShopRepository(ICaeriusNetDbContext Db) : IOfflineShopRepository
{
    public async ValueTask<(OfflineShopRowDto? Shop, IReadOnlyList<OfflineShopItemRowDto> Items)> GetByCharacterAsync(
        int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_OfflineShop_GetByCharacter", 32)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        var (shops, items) =
            await Db.QueryMultipleReadOnlyCollectionAsync<OfflineShopRowDto, OfflineShopItemRowDto>(sp, ct);
        return (shops.Count > 0 ? shops[0] : null, items);
    }

    // Deliberately uncached (caching-opportunities-audit, basse): SearchShopListingsService calls this on
    // every SearchShopListingsRequest with no client-side throttle (unlike HeroRankingService's 2.5s one),
    // so real invocation frequency is unconfirmed -- check sys.query_store_runtime_stats' execution_count
    // for usp_OfflineShop_GetAllOpen before adding AddInMemoryCache here. A cached "still for sale" row also
    // outlives an already-sold item until TTL expiry, a worse staleness cost than a security-gate false
    // negative, so this is not a reflexive 2s-TTL candidate like Ban/GmAllowlist/FirewallRule/MacRestriction.
    public async ValueTask<ReadOnlyCollection<OfflineShopOpenListingRowDto>> GetAllOpenAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_OfflineShop_GetAllOpen", 64).Build();
        return await Db.QueryAsReadOnlyCollectionAsync<OfflineShopOpenListingRowDto>(sp, ct);
    }

    public async ValueTask OpenAndReplaceContainersAsync(
        int characterId, short? zoneNumber, int shopDate, string shopName, int locationX, int locationY, int locationZ,
        IReadOnlyList<OfflineShopItemSlotTvp> items,
        IReadOnlyList<CharacterItemSlotTvp> inventoryPage0, IReadOnlyList<CharacterItemSlotTvp> inventoryPage1,
        CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_OfflineShop_OpenAndReplaceContainers", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("ZoneNumber", (object?)zoneNumber ?? DBNull.Value, SqlDbType.SmallInt)
            .AddParameter("ShopDate", shopDate, SqlDbType.Int)
            .AddParameter("ShopName", shopName, SqlDbType.NVarChar)
            .AddParameter("LocationX", locationX, SqlDbType.Int)
            .AddParameter("LocationY", locationY, SqlDbType.Int)
            .AddParameter("LocationZ", locationZ, SqlDbType.Int);

        if (items.Count > 0) builder.AddTvpParameter("Items", items);
        if (inventoryPage0.Count > 0) builder.AddTvpParameter("InventoryPage0", inventoryPage0);
        if (inventoryPage1.Count > 0) builder.AddTvpParameter("InventoryPage1", inventoryPage1);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask SetStateAsync(int characterId, byte shopState, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_OfflineShop_SetState", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("ShopState", shopState, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask RetrieveItemAndReplaceContainerAsync(int characterId, short slotIndex, int expectedItemId,
        int expectedQuantity, int expectedValue, byte container, IReadOnlyList<CharacterItemSlotTvp> items,
        CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_OfflineShop_RetrieveItemAndReplaceContainer", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("SlotIndex", slotIndex, SqlDbType.SmallInt)
            .AddParameter("ExpectedItemId", expectedItemId, SqlDbType.Int)
            .AddParameter("ExpectedQuantity", expectedQuantity, SqlDbType.Int)
            .AddParameter("ExpectedValue", expectedValue, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        if (items.Count > 0) builder.AddTvpParameter("Items", items);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask ExecutePurchaseAsync(int sellerCharacterId, short slotIndex, int expectedItemId,
        int expectedQuantity, int expectedValue, int price, int buyerCharacterId, byte buyerContainer,
        IReadOnlyList<CharacterItemSlotTvp> buyerItems, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_OfflineShop_ExecutePurchase", 0)
            .AddParameter("SellerCharacterId", sellerCharacterId, SqlDbType.Int)
            .AddParameter("SlotIndex", slotIndex, SqlDbType.SmallInt)
            .AddParameter("ExpectedItemId", expectedItemId, SqlDbType.Int)
            .AddParameter("ExpectedQuantity", expectedQuantity, SqlDbType.Int)
            .AddParameter("ExpectedValue", expectedValue, SqlDbType.Int)
            .AddParameter("Price", price, SqlDbType.Int)
            .AddParameter("BuyerCharacterId", buyerCharacterId, SqlDbType.Int)
            .AddParameter("BuyerContainer", buyerContainer, SqlDbType.TinyInt);

        if (buyerItems.Count > 0) builder.AddTvpParameter("BuyerItems", buyerItems);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask WithdrawMoneyAsync(int characterId, int expectedMoney, int expectedBigMoney,
        int todayDate, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_OfflineShop_WithdrawMoney", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("ExpectedMoney", expectedMoney, SqlDbType.Int)
            .AddParameter("ExpectedBigMoney", expectedBigMoney, SqlDbType.Int)
            .AddParameter("TodayDate", todayDate, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ProxyShopNameRowDto?> GetProxyShopNameAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_ProxyShopName_GetByCharacter", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<ProxyShopNameRowDto>(sp, ct);
    }

    public async ValueTask SetProxyShopNameAsync(int characterId, string shopName, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_ProxyShopName_Set", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("ShopName", shopName, SqlDbType.NVarChar)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask ExtendRentalAsync(int characterId, int newShopDate, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_OfflineShop_ExtendRental", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("ShopDate", newShopDate, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask ExtendRentalAndReplaceContainerAsync(int characterId, int newShopDate, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        var builder =
            new StoredProcedureParametersBuilder("game", "usp_OfflineShop_ExtendRentalAndReplaceContainer", 0)
                .AddParameter("CharacterId", characterId, SqlDbType.Int)
                .AddParameter("ShopDate", newShopDate, SqlDbType.Int)
                .AddParameter("Container", container, SqlDbType.TinyInt);

        if (items.Count > 0)
            builder.AddTvpParameter("Items", items);

        await Db.ExecuteAsync(builder.Build(), ct);
    }
}
