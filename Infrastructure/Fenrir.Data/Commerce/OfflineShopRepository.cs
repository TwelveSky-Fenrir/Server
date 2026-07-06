using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;

namespace Fenrir.Data.Commerce;

// game.OfflineShops/OfflineShopItems/ProxyShopNames access -- the offline/deputy ("proxy") personal-shop-stall feature.
public sealed record OfflineShopRepository(ICaeriusNetDbContext Db) : IOfflineShopRepository
{
    /// <summary>CZ_GET_DEPUTY_PSHOP_SEND sort 1/2; shop row is null if never opened.</summary>
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

    /// <summary>
    ///     Atomically upserts the shop + item slots and removes the items from live inventory; refuses to open over
    ///     unclaimed value.
    /// </summary>
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

    /// <summary>Flips ShopState only -- items/money stay attached (verified legacy close semantics).</summary>
    public async ValueTask SetStateAsync(int characterId, byte shopState, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_OfflineShop_SetState", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("ShopState", shopState, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>Atomic CAS retrieve-to-inventory; throws SQL 50272 if the slot no longer matches or the shop isn't closed.</summary>
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

    /// <summary>
    ///     Atomic CAS purchase: buyer debit+grant, seller item removal+credit, one transaction. Throws SQL 50272
    ///     (stale/not open), 50222 (buyer funds), 50261 (buyer cap), 50273 (seller BigMoney cap).
    /// </summary>
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

    /// <summary>Atomic earnings withdrawal; throws SQL 50276 (stale/not closed/nothing to withdraw) or 50261 (money cap).</summary>
    public async ValueTask WithdrawMoneyAsync(int characterId, int expectedMoney, int expectedBigMoney,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_OfflineShop_WithdrawMoney", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("ExpectedMoney", expectedMoney, SqlDbType.Int)
            .AddParameter("ExpectedBigMoney", expectedBigMoney, SqlDbType.Int)
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

    /// <summary>No CAS/ROWCOUNT guard by design -- see the interface doc for why a no-op update is a success.</summary>
    public async ValueTask ExtendRentalAsync(int characterId, int newShopDate, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_OfflineShop_ExtendRental", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("ShopDate", newShopDate, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
