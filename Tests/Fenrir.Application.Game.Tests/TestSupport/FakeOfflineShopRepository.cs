using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeOfflineShopRepository : IOfflineShopRepository
{
    private OfflineShopOpenListingRowDto[] _openListings = [];
    private OfflineShopRowDto? _shop;

    public bool ThrowOnExtendRental { get; set; }
    public int? LastExtendRentalNewShopDate { get; private set; }

    public ValueTask<(OfflineShopRowDto? Shop, IReadOnlyList<OfflineShopItemRowDto> Items)> GetByCharacterAsync(
        int characterId, CancellationToken ct)
    {
        return ValueTask.FromResult<(OfflineShopRowDto?, IReadOnlyList<OfflineShopItemRowDto>)>((_shop, []));
    }

    public ValueTask<ReadOnlyCollection<OfflineShopOpenListingRowDto>> GetAllOpenAsync(CancellationToken ct)
    {
        return ValueTask.FromResult(new ReadOnlyCollection<OfflineShopOpenListingRowDto>(_openListings));
    }

    public ValueTask OpenAndReplaceContainersAsync(int characterId, short? zoneNumber, int shopDate,
        string shopName, int locationX, int locationY, int locationZ, IReadOnlyList<OfflineShopItemSlotTvp> items,
        IReadOnlyList<CharacterItemSlotTvp> inventoryPage0, IReadOnlyList<CharacterItemSlotTvp> inventoryPage1,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetStateAsync(int characterId, byte shopState, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask RetrieveItemAndReplaceContainerAsync(int characterId, short slotIndex, int expectedItemId,
        int expectedQuantity, int expectedValue, byte container, IReadOnlyList<CharacterItemSlotTvp> items,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask ExecutePurchaseAsync(int sellerCharacterId, short slotIndex, int expectedItemId,
        int expectedQuantity, int expectedValue, int price, int buyerCharacterId, byte buyerContainer,
        IReadOnlyList<CharacterItemSlotTvp> buyerItems, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask WithdrawMoneyAsync(int characterId, int expectedMoney, int expectedBigMoney, int todayDate,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ProxyShopNameRowDto?> GetProxyShopNameAsync(int characterId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetProxyShopNameAsync(int characterId, string shopName, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask ExtendRentalAsync(int characterId, int newShopDate, CancellationToken ct)
    {
        if (ThrowOnExtendRental)
            throw new InvalidOperationException("Simulated SQL failure");

        LastExtendRentalNewShopDate = newShopDate;
        return ValueTask.CompletedTask;
    }

    public ValueTask ExtendRentalAndReplaceContainerAsync(int characterId, int newShopDate, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public void SeedShop(OfflineShopRowDto shop)
    {
        _shop = shop;
    }

    public void SeedOpenListings(params OfflineShopOpenListingRowDto[] rows)
    {
        _openListings = rows;
    }
}
