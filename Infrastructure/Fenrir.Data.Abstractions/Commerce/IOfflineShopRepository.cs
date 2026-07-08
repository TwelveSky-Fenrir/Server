using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Abstractions.Commerce;

public interface IOfflineShopRepository
{
    public ValueTask<(OfflineShopRowDto? Shop, IReadOnlyList<OfflineShopItemRowDto> Items)> GetByCharacterAsync(
        int characterId, CancellationToken ct);

    /// <summary>
    ///     Every for-sale slot across every currently open (ShopState=1) proxy shop, cluster-wide -- not
    ///     scoped to any one zone/shard, since the shared database is already the single store every proxy
    ///     shop persists through (the Fenrir-sharded equivalent of legacy's cross-instance shared-memory
    ///     proxy-shop table). Backs the market-search aggregator's proxy-shop half
    ///     (<c>CZ_PSHOP_ITEM_INFO_SEND</c>, Server/ts25zone/S04_MyWork02.cpp:6523-6558).
    /// </summary>
    public ValueTask<ReadOnlyCollection<OfflineShopOpenListingRowDto>> GetAllOpenAsync(CancellationToken ct);

    public ValueTask OpenAndReplaceContainersAsync(
        int characterId, short? zoneNumber, int shopDate, string shopName, int locationX, int locationY,
        int locationZ,
        IReadOnlyList<OfflineShopItemSlotTvp> items,
        IReadOnlyList<CharacterItemSlotTvp> inventoryPage0, IReadOnlyList<CharacterItemSlotTvp> inventoryPage1,
        CancellationToken ct);

    public ValueTask SetStateAsync(int characterId, byte shopState, CancellationToken ct);

    public ValueTask RetrieveItemAndReplaceContainerAsync(int characterId, short slotIndex, int expectedItemId,
        int expectedQuantity, int expectedValue, byte container, IReadOnlyList<CharacterItemSlotTvp> items,
        CancellationToken ct);

    public ValueTask ExecutePurchaseAsync(int sellerCharacterId, short slotIndex, int expectedItemId,
        int expectedQuantity, int expectedValue, int price, int buyerCharacterId, byte buyerContainer,
        IReadOnlyList<CharacterItemSlotTvp> buyerItems, CancellationToken ct);

    /// <summary>
    ///     Atomic earnings withdrawal. <paramref name="todayDate" /> is the caller's compact YYYYMMDD
    ///     "today" (<c>GameDate.Today()</c>), used to reject an expired shop the same way
    ///     <c>Zone.ProxyShops</c>'s own periodic sweep does (<c>entry.ShopDate &lt; today</c>) -- see
    ///     <see cref="OfflineShopRepository.WithdrawMoneyAsync" /> for the full THROW-number contract.
    /// </summary>
    public ValueTask WithdrawMoneyAsync(int characterId, int expectedMoney, int expectedBigMoney, int todayDate,
        CancellationToken ct);

    public ValueTask<ProxyShopNameRowDto?> GetProxyShopNameAsync(int characterId, CancellationToken ct);

    public ValueTask SetProxyShopNameAsync(int characterId, string shopName, CancellationToken ct);

    /// <summary>
    ///     Extends a proxy shop's rental expiration (game.OfflineShops.ShopDate) to a freshly computed
    ///     compact-date value. Succeeds identically whether a matching row was updated or none exists at all
    ///     (no <c>@@ROWCOUNT</c> guard) -- mirrors the legacy's own "reports success with nothing persisted"
    ///     behavior for a character with no persisted shop record (Server/ts25extra/S08_MyDB.cpp:1085-1106).
    /// </summary>
    public ValueTask ExtendRentalAsync(int characterId, int newShopDate, CancellationToken ct);
}
