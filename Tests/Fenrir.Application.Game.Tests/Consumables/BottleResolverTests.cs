using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Tests.Consumables;

public class BottleResolverTests
{
    private static ImmutableArray<(int ItemId, int Count)> Slots(params (int ItemId, int Count)[] slots)
    {
        var array = new (int, int)[BottleResolver.SlotCount];
        for (var i = 0; i < slots.Length; i++)
            array[i] = slots[i];
        return [..array];
    }

    [Fact]
    public void Acquire_EmptySlots_ClaimsFirstSlot()
    {
        var result = BottleResolver.ResolveAcquire(Slots(), 500);

        Assert.Equal(BottleResolver.AcquireOutcome.Success, result.Outcome);
        Assert.Equal(0, result.SlotIndex);
        Assert.Equal(BottleResolver.RefillCount, result.RefilledCount);
    }

    [Fact]
    public void Acquire_SkipsOccupiedSlots_ClaimsFirstEmpty()
    {
        var result = BottleResolver.ResolveAcquire(Slots((100, 5), (200, 3)), 500);

        Assert.Equal(BottleResolver.AcquireOutcome.Success, result.Outcome);
        Assert.Equal(2, result.SlotIndex);
    }

    [Fact]
    public void Acquire_ExistingSlotSameItemStillHasStock_IsRejected()
    {
        var result = BottleResolver.ResolveAcquire(Slots((500, 1)), 500);

        Assert.Equal(BottleResolver.AcquireOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Acquire_ExistingSlotSameItemDepleted_IsReused()
    {
        var result = BottleResolver.ResolveAcquire(Slots((100, 5), (500, 0)), 500);

        Assert.Equal(BottleResolver.AcquireOutcome.Success, result.Outcome);
        Assert.Equal(1, result.SlotIndex);
    }

    [Fact]
    public void Acquire_DepletedSlotOfSameItem_DoesNotShadowAnEarlierEmptySlot()
    {
        var result = BottleResolver.ResolveAcquire(Slots((0, 0), (500, 0)), 500);

        Assert.Equal(BottleResolver.AcquireOutcome.Success, result.Outcome);
        Assert.Equal(0, result.SlotIndex);
    }

    [Fact]
    public void Acquire_AllSlotsOccupied_IsRejected()
    {
        var slots = Slots((1, 1), (2, 1), (3, 1), (4, 1), (5, 1), (6, 1), (7, 1), (8, 1), (9, 1), (10, 1));
        var result = BottleResolver.ResolveAcquire(slots, 999);

        Assert.Equal(BottleResolver.AcquireOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Drink_SortNotZero_IsRejected()
    {
        var result = BottleResolver.ResolveDrink(Slots((500, 5)), 1, 0);

        Assert.Equal(BottleResolver.DrinkOutcome.Rejected, result.Outcome);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void Drink_IndexOutOfRange_IsRejected(int index)
    {
        var result = BottleResolver.ResolveDrink(Slots((500, 5)), 0, index);

        Assert.Equal(BottleResolver.DrinkOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Drink_EmptySlot_IsSilent()
    {
        var result = BottleResolver.ResolveDrink(Slots(), 0, 0);

        Assert.Equal(BottleResolver.DrinkOutcome.Silent, result.Outcome);
    }

    [Fact]
    public void Drink_ZeroStock_IsSilent()
    {
        var result = BottleResolver.ResolveDrink(Slots((500, 0)), 0, 0);

        Assert.Equal(BottleResolver.DrinkOutcome.Silent, result.Outcome);
    }

    [Fact]
    public void Drink_Success_DecrementsCount()
    {
        var result = BottleResolver.ResolveDrink(Slots((500, 5)), 0, 0);

        Assert.Equal(BottleResolver.DrinkOutcome.Success, result.Outcome);
        Assert.Equal(4, result.NewCount);
    }

    [Fact]
    public void Drink_PetBlocksDrinking_IsRejected_EvenWithAChargedSlot()
    {
        var result = BottleResolver.ResolveDrink(Slots((500, 5)), 0, 0, petBlocksDrinking: true);

        Assert.Equal(BottleResolver.DrinkOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Drink_PetBlocksDrinking_TakesPrecedenceOverAnEmptySlot()
    {
        var result = BottleResolver.ResolveDrink(Slots(), 0, 0, petBlocksDrinking: true);

        Assert.Equal(BottleResolver.DrinkOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Drink_NoPetBlocking_StillSucceeds()
    {
        var result = BottleResolver.ResolveDrink(Slots((500, 5)), 0, 0, petBlocksDrinking: false);

        Assert.Equal(BottleResolver.DrinkOutcome.Success, result.Outcome);
    }
}
