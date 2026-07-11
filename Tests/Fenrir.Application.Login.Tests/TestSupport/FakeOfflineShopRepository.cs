using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeOfflineShopRepository : IOfflineShopRepository
{
    private readonly Dictionary<int, (OfflineShopRowDto? Shop, IReadOnlyList<OfflineShopItemRowDto> Items)>
        _byCharacterId = new();

    public List<int> QueriedCharacterIds { get; } = [];

    public ValueTask<(OfflineShopRowDto? Shop, IReadOnlyList<OfflineShopItemRowDto> Items)> GetByCharacterAsync(
        int characterId, CancellationToken ct)
    {
        QueriedCharacterIds.Add(characterId);

        if (_byCharacterId.TryGetValue(characterId, out var entry))
            return ValueTask.FromResult(entry);

        return ValueTask.FromResult<(OfflineShopRowDto? Shop, IReadOnlyList<OfflineShopItemRowDto> Items)>(
            (null, Array.Empty<OfflineShopItemRowDto>()));
    }

    public ValueTask<ReadOnlyCollection<OfflineShopOpenListingRowDto>> GetAllOpenAsync(CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask OpenAndReplaceContainersAsync(int characterId, short? zoneNumber, int shopDate,
        string shopName, int locationX, int locationY, int locationZ,
        IReadOnlyList<OfflineShopItemSlotTvp> items, IReadOnlyList<CharacterItemSlotTvp> inventoryPage0,
        IReadOnlyList<CharacterItemSlotTvp> inventoryPage1, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask SetStateAsync(int characterId, byte shopState, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask RetrieveItemAndReplaceContainerAsync(int characterId, short slotIndex, int expectedItemId,
        int expectedQuantity, int expectedValue, byte container, IReadOnlyList<CharacterItemSlotTvp> items,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask ExecutePurchaseAsync(int sellerCharacterId, short slotIndex, int expectedItemId,
        int expectedQuantity, int expectedValue, int price, int buyerCharacterId, byte buyerContainer,
        IReadOnlyList<CharacterItemSlotTvp> buyerItems, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask WithdrawMoneyAsync(int characterId, int expectedMoney, int expectedBigMoney, int todayDate,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<ProxyShopNameRowDto?> GetProxyShopNameAsync(int characterId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask SetProxyShopNameAsync(int characterId, string shopName, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask ExtendRentalAsync(int characterId, int newShopDate, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public static FakeOfflineShopRepository Empty()
    {
        return new FakeOfflineShopRepository();
    }

    public static FakeOfflineShopRepository With(int characterId, OfflineShopRowDto? shop,
        params OfflineShopItemRowDto[] items)
    {
        var repository = new FakeOfflineShopRepository();
        repository._byCharacterId[characterId] = (shop, items);
        return repository;
    }
}
