using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.Inventory;

public class GroundItemPickupPolicyTests
{
    private static GroundItemEntity Item(int itemId, int quantity, int value = 0)
    {
        return new GroundItemEntity(1, 1u, itemId, quantity, value, 0, 0, 0, 0,
            "Killer", "", 0, TimeSpan.Zero,
            0, 0, 0);
    }

    private static ItemDefinition MoneyItem(int itemId)
    {
        return new ItemDefinition(WorldDataTestRows.Item(itemId) with { Sort = 1 }, []);
    }

    private static ItemDefinition StackableItem(int itemId)
    {
        return new ItemDefinition(WorldDataTestRows.Item(itemId) with { Sort = 2 }, []);
    }

    private static ItemDefinition EquipmentItem(int itemId)
    {
        return new ItemDefinition(WorldDataTestRows.Item(itemId) with { Sort = 9 }, []);
    }

    [Fact]
    public void MoneyItem_ResolvesToMoneyOutcome_RegardlessOfDestinationSlot()
    {
        var result = GroundItemPickupPolicy.Resolve(MoneyItem(1), Item(1, 5000), null);

        Assert.Equal(GroundItemPickupPolicy.Outcome.Money, result.Outcome);
        Assert.Equal(5000, result.MoneyAmount);
        Assert.Null(result.NewSlot);
    }

    [Fact]
    public void StackableItem_EmptyDestination_IsPlaced()
    {
        var result = GroundItemPickupPolicy.Resolve(StackableItem(50), Item(50, 10), null);

        Assert.Equal(GroundItemPickupPolicy.Outcome.Placed, result.Outcome);
        Assert.Equal(50, result.NewSlot!.Value.ItemId);
        Assert.Equal(10, result.NewSlot.Value.Quantity);
    }

    [Fact]
    public void StackableItem_SameItemAlreadyPresent_Merges()
    {
        var existing = new ItemStack(50, 20, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var result = GroundItemPickupPolicy.Resolve(StackableItem(50), Item(50, 10), existing);

        Assert.Equal(GroundItemPickupPolicy.Outcome.Stacked, result.Outcome);
        Assert.Equal(30, result.NewSlot!.Value.Quantity);
    }

    [Fact]
    public void StackableItem_MergeWouldExceedCap_IsRejected()
    {
        var existing = new ItemStack(50, 995, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var result = GroundItemPickupPolicy.Resolve(StackableItem(50), Item(50, 10), existing);

        Assert.Equal(GroundItemPickupPolicy.Outcome.Rejected, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void StackableItem_DifferentItemOccupyingDestination_IsRejected()
    {
        var existing = new ItemStack(999, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var result = GroundItemPickupPolicy.Resolve(StackableItem(50), Item(50, 10), existing);

        Assert.Equal(GroundItemPickupPolicy.Outcome.Rejected, result.Outcome);
    }

    [Fact]
    public void EquipmentItem_EmptyDestination_IsPlacedWithQuantityOne()
    {
        var result = GroundItemPickupPolicy.Resolve(EquipmentItem(700), Item(700, 0), null);

        Assert.Equal(GroundItemPickupPolicy.Outcome.Placed, result.Outcome);
        Assert.Equal(1, result.NewSlot!.Value.Quantity);
    }

    [Fact]
    public void EquipmentItem_OccupiedDestination_IsRejected_NoSwapFallback()
    {
        var existing = new ItemStack(1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var result = GroundItemPickupPolicy.Resolve(EquipmentItem(700), Item(700, 0), existing);

        Assert.Equal(GroundItemPickupPolicy.Outcome.Rejected, result.Outcome);
    }

    [Fact]
    public void EquipmentItem_PreservesEnchantCombineRefineSocketFromPackedValue()
    {
        var packed = ItemValueCodec.Encode(12, 3, 1, 2);

        var result = GroundItemPickupPolicy.Resolve(EquipmentItem(700), Item(700, 0, packed), null);

        Assert.Equal(12, result.NewSlot!.Value.Enchant);
        Assert.Equal(3, result.NewSlot.Value.Combine);
        Assert.Equal(1, result.NewSlot.Value.Refine);
        Assert.Equal(2, result.NewSlot.Value.Socket);
    }
}
