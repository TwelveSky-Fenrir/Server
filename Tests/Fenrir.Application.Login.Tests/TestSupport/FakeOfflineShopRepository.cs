using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;

namespace Fenrir.Application.Login.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for IOfflineShopRepository, used only for DeleteAvatarService's proxy-shop refusal
///     check -- every member besides <see cref="GetByCharacterAsync" /> throws, since nothing else on this path
///     calls them.
/// </summary>
internal sealed class FakeOfflineShopRepository : IOfflineShopRepository
{
    private readonly Dictionary<int, (OfflineShopRowDto? Shop, IReadOnlyList<OfflineShopItemRowDto> Items)>
        _byCharacterId = new();

    /// <summary>Every characterId GetByCharacterAsync was called with, in call order -- proves ordering/short-circuit.</summary>
    public List<int> QueriedCharacterIds { get; } = [];

    /// <summary>No character has ever opened a shop.</summary>
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

    public ValueTask<(OfflineShopRowDto? Shop, IReadOnlyList<OfflineShopItemRowDto> Items)> GetByCharacterAsync(
        int characterId, CancellationToken ct)
    {
        QueriedCharacterIds.Add(characterId);

        if (_byCharacterId.TryGetValue(characterId, out var entry))
            return ValueTask.FromResult(entry);

        return ValueTask.FromResult<(OfflineShopRowDto? Shop, IReadOnlyList<OfflineShopItemRowDto> Items)>(
            (null, Array.Empty<OfflineShopItemRowDto>()));
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

    public ValueTask WithdrawMoneyAsync(int characterId, int expectedMoney, int expectedBigMoney,
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
}
