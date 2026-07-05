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
}
