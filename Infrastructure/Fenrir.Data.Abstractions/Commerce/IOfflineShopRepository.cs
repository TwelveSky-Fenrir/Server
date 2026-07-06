using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Abstractions.Commerce;

public interface IOfflineShopRepository
{
    public ValueTask<(OfflineShopRowDto? Shop, IReadOnlyList<OfflineShopItemRowDto> Items)> GetByCharacterAsync(
        int characterId, CancellationToken ct);

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

    public ValueTask WithdrawMoneyAsync(int characterId, int expectedMoney, int expectedBigMoney,
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
