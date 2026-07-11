using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Social.Pshop;

public class PshopPurchasePolicyTests
{
    private static ItemDefinition Item(int itemId, byte sort)
    {
        var row = new ItemRowDto(
            itemId, $"Item{itemId}", null, null, null,
            0, sort, 0, 0, 0,
            1, 0, 0, 0,
            0, 0, 0, 1, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0,
            0, 0, null,
            0, 0, 0, 0, 0);
        return new ItemDefinition(row, []);
    }

    private static PshopInfo EmptyListing()
    {
        return new PshopInfo { UniqueNumber = 1, Name = "Shop", ItemInfo = new int[225], SocketInfo = new int[75] };
    }

    private static PshopInfo WithSlot(int page, int slot, int itemId, int quantity, int value, int serial,
        int price, int invPage, int invIndex, int posX, int posY)
    {
        var info = EmptyListing();
        var i = PshopPurchasePolicy.FlatIndex(page, slot);
        info.ItemInfo[i + 0] = itemId;
        info.ItemInfo[i + 1] = quantity;
        info.ItemInfo[i + 2] = value;
        info.ItemInfo[i + 3] = serial;
        info.ItemInfo[i + 4] = price;
        info.ItemInfo[i + 5] = invPage;
        info.ItemInfo[i + 6] = invIndex;
        info.ItemInfo[i + 7] = posX;
        info.ItemInfo[i + 8] = posY;
        return info;
    }

    [Fact]
    public void ReadSlot_DecodesTheNineFieldFlatLayout()
    {
        var info = WithSlot(1, 2, 100, 5, 0, 7, 999, 0, 3,
            1, 2);

        var slot = PshopPurchasePolicy.ReadSlot(info, 1, 2);

        Assert.True(slot.IsOccupied);
        Assert.Equal(100, slot.ItemId);
        Assert.Equal(5, slot.Quantity);
        Assert.Equal(7, slot.Serial);
        Assert.Equal(999, slot.Price);
        Assert.Equal(0, slot.InventoryPage);
        Assert.Equal(3, slot.InventoryIndex);
    }

    [Fact]
    public void ReadSlot_EmptySlot_IsNotOccupied()
    {
        var slot = PshopPurchasePolicy.ReadSlot(EmptyListing(), 0, 0);

        Assert.False(slot.IsOccupied);
    }

    [Fact]
    public void ValidateOpenSlot_LiveInventoryMatchesAdvertisedValues_Succeeds()
    {
        var itemDefinition = Item(100, 3);
        var slot = PshopPurchasePolicy.ReadSlot(
            WithSlot(0, 0, 100, 1, 0, 5, 500, 0, 0, 0, 0),
            0, 0);
        var liveStack = new ItemStack(100, 1, 0, 0, 0, 0, 0, 0, 0, 0, 5);

        Assert.Equal(PshopPurchasePolicy.OpenSlotOutcome.Success,
            PshopPurchasePolicy.ValidateOpenSlot(slot, itemDefinition, liveStack));
    }

    [Fact]
    public void ValidateOpenSlot_PriceOutOfRange_IsRejected()
    {
        var itemDefinition = Item(100, 3);
        var slot = PshopPurchasePolicy.ReadSlot(
            WithSlot(0, 0, 100, 1, 0, 5, 0, 0, 0, 0, 0), 0, 0);
        var liveStack = new ItemStack(100, 1, 0, 0, 0, 0, 0, 0, 0, 0, 5);

        Assert.Equal(PshopPurchasePolicy.OpenSlotOutcome.PriceOutOfRange,
            PshopPurchasePolicy.ValidateOpenSlot(slot, itemDefinition, liveStack));
    }

    [Fact]
    public void ValidateOpenSlot_LiveInventoryDoesNotMatch_IsRejected()
    {
        var itemDefinition = Item(100, 3);
        var slot = PshopPurchasePolicy.ReadSlot(
            WithSlot(0, 0, 100, 1, 0, 5, 500, 0, 0, 0, 0), 0, 0);
        var staleLiveStack = new ItemStack(100, 2, 0, 0, 0, 0, 0, 0, 0, 0, 5);

        Assert.Equal(PshopPurchasePolicy.OpenSlotOutcome.InventoryMismatch,
            PshopPurchasePolicy.ValidateOpenSlot(slot, itemDefinition, staleLiveStack));
    }

    [Fact]
    public void ValidateOpenSlot_UnknownItem_IsRejected()
    {
        var slot = PshopPurchasePolicy.ReadSlot(
            WithSlot(0, 0, 100, 1, 0, 5, 500, 0, 0, 0, 0), 0, 0);

        Assert.Equal(PshopPurchasePolicy.OpenSlotOutcome.UnknownItem,
            PshopPurchasePolicy.ValidateOpenSlot(slot, null, new ItemStack(100, 1, 0, 0, 0, 0, 0, 0, 0, 0, 5)));
    }

    [Fact]
    public void ValidateOpenSlot_ItemCheckAvatarShopEqualsOne_IsBarredFromShopSale()
    {
        var itemDefinition = new ItemDefinition(Item(100, 3).Item with { CheckAvatarShop = 1 }, []);
        var slot = PshopPurchasePolicy.ReadSlot(
            WithSlot(0, 0, 100, 1, 0, 5, 500, 0, 0, 0, 0), 0, 0);
        var liveStack = new ItemStack(100, 1, 0, 0, 0, 0, 0, 0, 0, 0, 5);

        Assert.Equal(PshopPurchasePolicy.OpenSlotOutcome.BarredFromShopSale,
            PshopPurchasePolicy.ValidateOpenSlot(slot, itemDefinition, liveStack));
    }

    [Fact]
    public void ValidateOpenSlot_ItemCheckAvatarShopEqualsTwo_PassesThisCheck()
    {
        var itemDefinition = new ItemDefinition(Item(100, 3).Item with { CheckAvatarShop = 2 }, []);
        var slot = PshopPurchasePolicy.ReadSlot(
            WithSlot(0, 0, 100, 1, 0, 5, 500, 0, 0, 0, 0), 0, 0);
        var liveStack = new ItemStack(100, 1, 0, 0, 0, 0, 0, 0, 0, 0, 5);

        Assert.Equal(PshopPurchasePolicy.OpenSlotOutcome.Success,
            PshopPurchasePolicy.ValidateOpenSlot(slot, itemDefinition, liveStack));
    }

    [Fact]
    public void ResolvePurchase_EmptyDestination_PlacesTheFullListedStack()
    {
        var itemDefinition = Item(100, 3);
        var slot = PshopPurchasePolicy.ReadSlot(
            WithSlot(0, 0, 100, 1, 0, 5, 500, 0, 0, 0, 0), 0, 0);

        var result = PshopPurchasePolicy.ResolvePurchase(slot, itemDefinition, null);

        Assert.True(result.Succeeded);
        Assert.Equal(100, result.NewDestinationStack!.Value.ItemId);
        Assert.Equal(1, result.NewDestinationStack.Value.Quantity);
    }

    [Fact]
    public void ResolvePurchase_StackableMergeIntoSameItem_Succeeds()
    {
        var itemDefinition = Item(1019, 2);
        var slot = PshopPurchasePolicy.ReadSlot(
            WithSlot(0, 0, 1019, 10, 0, 0, 100, 0, 0, 0, 0),
            0, 0);
        var destination = new ItemStack(1019, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var result = PshopPurchasePolicy.ResolvePurchase(slot, itemDefinition, destination);

        Assert.True(result.Succeeded);
        Assert.Equal(15, result.NewDestinationStack!.Value.Quantity);
    }

    [Fact]
    public void ResolvePurchase_DestinationHoldsADifferentItem_IsRejected()
    {
        var itemDefinition = Item(100, 3);
        var slot = PshopPurchasePolicy.ReadSlot(
            WithSlot(0, 0, 100, 1, 0, 5, 500, 0, 0, 0, 0), 0, 0);
        var destination = new ItemStack(999, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var result = PshopPurchasePolicy.ResolvePurchase(slot, itemDefinition, destination);

        Assert.Equal(PshopPurchasePolicy.PurchaseOutcome.DestinationConflict, result.Outcome);
    }

    [Fact]
    public void ResolvePurchase_StackableMergeExceedingCap_IsRejected()
    {
        var itemDefinition = Item(1019, 2);
        var slot = PshopPurchasePolicy.ReadSlot(
            WithSlot(0, 0, 1019, 500, 0, 0, 100, 0, 0, 0, 0),
            0, 0);
        var destination = new ItemStack(1019, GroundItemPickupPolicy.MaxStackQuantity - 100, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var result = PshopPurchasePolicy.ResolvePurchase(slot, itemDefinition, destination);

        Assert.Equal(PshopPurchasePolicy.PurchaseOutcome.DestinationConflict, result.Outcome);
    }
}
