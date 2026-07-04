using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Characters;

namespace Fenrir.Data.Commerce;

/// <summary>
///     game.OfflineShops/OfflineShopItems/ProxyShopNames access (V8 Player Commerce &amp; Cash) -- the
///     offline/deputy ("proxy") personal-shop-stall feature (contracts/04_commerce.md CZ 108-110/ZC
///     134-137). Singleton, injected only with <see cref="ICaeriusNetDbContext" />, same posture as every
///     other repository in this assembly (architecture reference §11.1).
/// </summary>
public sealed record OfflineShopRepository(ICaeriusNetDbContext Db) : IOfflineShopRepository
{
    /// <summary>Own-view read (CZ_GET_DEPUTY_PSHOP_SEND sort 1/2) -- the shop row (null if never opened) plus every occupied slot.</summary>
    public async ValueTask<(OfflineShopRowDto? Shop, IReadOnlyList<OfflineShopItemRowDto> Items)> GetByCharacterAsync(
        int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_OfflineShop_GetByCharacter", 32)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        var (shops, items) = await Db.QueryMultipleReadOnlyCollectionAsync<OfflineShopRowDto, OfflineShopItemRowDto>(sp, ct);
        return (shops.Count > 0 ? shops[0] : null, items);
    }

    /// <summary>
    ///     Atomically opens the shop (upsert row + item slots) and removes the listed items from the
    ///     owner's live inventory (usp_OfflineShop_OpenAndReplaceContainers, D7 regime (b)) -- see that
    ///     proc's own header for the "refuses to open over unclaimed value" guard.
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

    /// <summary>Flips ShopState only -- items/money stay attached (usp_OfflineShop_SetState, verified legacy close semantics).</summary>
    public async ValueTask SetStateAsync(int characterId, byte shopState, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_OfflineShop_SetState", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("ShopState", shopState, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>Atomic CAS retrieve-one-item-back-to-inventory (usp_OfflineShop_RetrieveItemAndReplaceContainer). Throws SQL 50272 if the slot no longer matches or the shop is not closed.</summary>
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
    ///     Atomic CAS purchase commit (usp_OfflineShop_ExecutePurchase) -- buyer money debit + item grant,
    ///     seller shop item removal + earnings credit, all in one transaction. Throws SQL 50272 (stale
    ///     listing/shop not open), 50222 (buyer funds), 50261 (buyer cap), or 50273 (seller BigMoney cap).
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

    /// <summary>Atomic earnings withdrawal (usp_OfflineShop_WithdrawMoney) -- throws SQL 50276 (stale/not closed/nothing to withdraw) or 50261 (owner's own money cap).</summary>
    public async ValueTask WithdrawMoneyAsync(int characterId, int expectedMoney, int expectedBigMoney, CancellationToken ct)
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
}
