using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Inventory;

/// <summary>
///     Bounds and move-mechanics coverage for <see cref="ContainerMatrix" />, the pure policy consumed by
///     <c>GenericActionHandler</c>.
/// </summary>
public class ContainerMatrixTests
{
    [Theory]
    [InlineData(208)] // inventory<->inventory -- implemented
    [InlineData(210)] // inventory->equipment -- implemented
    [InlineData(213)] // equipment->inventory -- implemented
    [InlineData(201)] // pickup -- known, not a container move, not implemented here
    [InlineData(501)] // GM command -- known, not implemented
    [InlineData(223)] // inventory<->store -- known container-move family, not implemented yet
    public void IsKnownSort_EveryLegacyFamilyMember_ReturnsTrue(int sort)
    {
        Assert.True(ContainerMatrix.IsKnownSort(sort));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(999999)]
    public void IsKnownSort_AbsentFromEveryLegacyFamily_ReturnsFalse(int sort)
    {
        Assert.False(ContainerMatrix.IsKnownSort(sort));
    }

    [Theory]
    [InlineData(208)]
    [InlineData(210)]
    [InlineData(213)]
    public void IsImplementedContainerMoveSort_TheThreeFundamentalFamilies_ReturnsTrue(int sort)
    {
        Assert.True(ContainerMatrix.IsImplementedContainerMoveSort(sort));
    }

    [Theory]
    [InlineData(201)] // known, but not a container move at all
    [InlineData(223)] // known container move, but Store is out of this pass's scope
    [InlineData(211)] // known container move, but hotkey-assign is out of this pass's scope
    public void IsImplementedContainerMoveSort_KnownButOutOfScope_ReturnsFalse(int sort)
    {
        Assert.False(ContainerMatrix.IsImplementedContainerMoveSort(sort));
    }

    [Theory]
    [InlineData(ContainerMatrix.InventoryPage0, 0, true)]
    [InlineData(ContainerMatrix.InventoryPage0, 63, true)]
    [InlineData(ContainerMatrix.InventoryPage0, 64, false)]
    [InlineData(ContainerMatrix.InventoryPage1, 63, true)]
    [InlineData(ContainerMatrix.Equipment, 0, true)]
    [InlineData(ContainerMatrix.Equipment, 12, true)]
    [InlineData(ContainerMatrix.Equipment, 13, false)]
    [InlineData(ContainerMatrix.StorePage0, 27, true)]
    [InlineData(ContainerMatrix.StorePage0, 28, false)]
    [InlineData((byte)99, 0, false)] // unknown container id
    public void IsValidSlot_RespectsPerContainerBounds(byte container, int slot, bool expected)
    {
        Assert.Equal(expected, ContainerMatrix.IsValidSlot(container, slot));
    }

    [Fact]
    public void IsValidSlot_NegativeSlot_IsAlwaysInvalid()
    {
        Assert.False(ContainerMatrix.IsValidSlot(ContainerMatrix.InventoryPage0, -1));
    }

    [Fact]
    public void TryResolveContainers_Sort208_BothPagesValid_ResolvesInventoryToInventory()
    {
        var ok = ContainerMatrix.TryResolveContainers(208, ContainerMatrix.InventoryPage0,
            ContainerMatrix.InventoryPage1, out var from, out var to);

        Assert.True(ok);
        Assert.Equal(ContainerMatrix.InventoryPage0, from);
        Assert.Equal(ContainerMatrix.InventoryPage1, to);
    }

    [Fact]
    public void TryResolveContainers_Sort210_IgnoresPage2_AlwaysTargetsEquipment()
    {
        // page2 arg is a placeholder -- Equipment has no pages
        var ok = ContainerMatrix.TryResolveContainers(210, ContainerMatrix.InventoryPage1, 0, out var from,
            out var to);

        Assert.True(ok);
        Assert.Equal(ContainerMatrix.InventoryPage1, from);
        Assert.Equal(ContainerMatrix.Equipment, to);
    }

    [Fact]
    public void TryResolveContainers_Sort213_SourceIsAlwaysEquipment()
    {
        // page1 arg is a placeholder -- Equipment has no pages
        var ok = ContainerMatrix.TryResolveContainers(213, 0, ContainerMatrix.InventoryPage0, out var from,
            out var to);

        Assert.True(ok);
        Assert.Equal(ContainerMatrix.Equipment, from);
        Assert.Equal(ContainerMatrix.InventoryPage0, to);
    }

    [Fact]
    public void TryResolveContainers_Sort208_PageOutOfRange_Fails()
    {
        var ok = ContainerMatrix.TryResolveContainers(208, 0, 2, out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryResolveContainers_UnrecognizedSort_Fails()
    {
        var ok = ContainerMatrix.TryResolveContainers(999, 0, 0, out _, out _);

        Assert.False(ok);
    }

    private static ItemStack Stack(int itemId, int quantity = 1, byte enchant = 0)
    {
        return new ItemStack(itemId, quantity, enchant, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    [Fact]
    public void ResolveMove_EmptyDestination_MovesWholeStack()
    {
        var source = Stack(1000, 5);

        var result = ContainerMatrix.ResolveMove(ContainerMatrix.InventoryPage0, 0, 0,
            ContainerMatrix.InventoryPage0, 1, source, null, false);

        Assert.Equal(ContainerMatrix.MoveOutcome.Success, result.Outcome);
        Assert.Null(result.NewSource);
        Assert.Equal(source, result.NewDestination);
    }

    [Fact]
    public void ResolveMove_EmptyDestination_PartialQuantity_SplitsTheStack()
    {
        var source = Stack(2, 10);

        var result = ContainerMatrix.ResolveMove(ContainerMatrix.InventoryPage0, 0, 4,
            ContainerMatrix.InventoryPage0, 1, source, null, true);

        Assert.Equal(ContainerMatrix.MoveOutcome.Success, result.Outcome);
        Assert.Equal(6, result.NewSource!.Value.Quantity);
        Assert.Equal(4, result.NewDestination!.Value.Quantity);
        Assert.Equal(2, result.NewDestination!.Value.ItemId);
    }

    [Fact]
    public void ResolveMove_SameStackableItem_Merges()
    {
        var source = Stack(500, 3);
        var destination = Stack(500, 7);

        var result = ContainerMatrix.ResolveMove(ContainerMatrix.InventoryPage0, 0, 0,
            ContainerMatrix.InventoryPage0, 1, source, destination, true);

        Assert.Equal(ContainerMatrix.MoveOutcome.Success, result.Outcome);
        Assert.Null(result.NewSource);
        Assert.Equal(10, result.NewDestination!.Value.Quantity);
    }

    [Fact]
    public void ResolveMove_DifferentItem_OccupiedDestinationIsRejected_NoSwap()
    {
        // Legacy's ProcessForInventoryToInventory/ProcessForInventoryToEquip/ProcessForEquipToInventory family
        // rejects an occupied, non-mergeable destination outright for all 3 directions -- there is no
        // swap-with-occupant concept anywhere in that family (Server/ts25zone/S04_MyWork05.cpp:875-880).
        var source = Stack(10);
        var destination = Stack(20);

        var result = ContainerMatrix.ResolveMove(ContainerMatrix.InventoryPage0, 0, 0,
            ContainerMatrix.InventoryPage0, 1, source, destination, false);

        Assert.Equal(ContainerMatrix.MoveOutcome.DestinationOccupied, result.Outcome);
        Assert.False(result.Succeeded);
        Assert.Equal(source, result.NewSource);
        Assert.Equal(destination, result.NewDestination);
    }

    [Fact]
    public void ResolveMove_SameItemIdButNotStackable_OccupiedDestinationIsRejected_NoSwap()
    {
        // sourceIsStackable=false: two enchanted items sharing an ItemId aren't one mergeable pile, so this
        // must reject exactly like any other occupied-destination case -- not merge, and (the historical bug
        // this test now guards against) not swap either. Server/ts25zone/S04_MyWork05.cpp:1589-1594 confirms
        // the equip->inventory (unequip) direction rejects identically; using Equipment as the source
        // container here is deliberately the same shape as the unequip (tSort 213) direction, where a swap
        // would otherwise write an arbitrary, unvalidated item straight into the vacated Equipment slot.
        var source = Stack(999, 1, 5);
        var destination = Stack(999, 1, 12);

        var result = ContainerMatrix.ResolveMove(ContainerMatrix.Equipment, 7, 0,
            ContainerMatrix.InventoryPage0, 1, source, destination, false);

        Assert.Equal(ContainerMatrix.MoveOutcome.DestinationOccupied, result.Outcome);
        Assert.False(result.Succeeded);
        Assert.Equal(source, result.NewSource);
        Assert.Equal(destination, result.NewDestination);
    }

    [Fact]
    public void ResolveMove_SameSlotToItself_IsNoOp()
    {
        var source = Stack(1);

        var result = ContainerMatrix.ResolveMove(ContainerMatrix.InventoryPage0, 5, 0,
            ContainerMatrix.InventoryPage0, 5, source, source, false);

        Assert.Equal(ContainerMatrix.MoveOutcome.NoOp, result.Outcome);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ResolveMove_EmptySource_Fails()
    {
        var result = ContainerMatrix.ResolveMove(ContainerMatrix.InventoryPage0, 0, 0,
            ContainerMatrix.InventoryPage0, 1, null, null, false);

        Assert.Equal(ContainerMatrix.MoveOutcome.SourceEmpty, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveMove_RequestedQuantityExceedsStack_Fails()
    {
        var source = Stack(1, 3);

        var result = ContainerMatrix.ResolveMove(ContainerMatrix.InventoryPage0, 0, 10,
            ContainerMatrix.InventoryPage0, 1, source, null, true);

        Assert.Equal(ContainerMatrix.MoveOutcome.InsufficientQuantity, result.Outcome);
    }

    [Fact]
    public void ResolveMove_SourceSlotOutOfRange_FailsBeforeReadingAnything()
    {
        var result = ContainerMatrix.ResolveMove(ContainerMatrix.Equipment, 13, 0,
            ContainerMatrix.InventoryPage0, 0, Stack(1), null, false);

        Assert.Equal(ContainerMatrix.MoveOutcome.SourceOutOfRange, result.Outcome);
    }

    [Fact]
    public void ResolveMove_DestinationSlotOutOfRange_Fails()
    {
        var result = ContainerMatrix.ResolveMove(ContainerMatrix.InventoryPage0, 0, 0,
            ContainerMatrix.Equipment, 13, Stack(1), null, false);

        Assert.Equal(ContainerMatrix.MoveOutcome.DestinationOutOfRange, result.Outcome);
    }

    [Fact]
    public void ApplyMove_SameContainer_BothSlotsUpdatedInOneProjection()
    {
        var source = Stack(1);
        var current = ImmutableDictionary<byte, ItemStack>.Empty.Add(0, source);

        var move = ContainerMatrix.ResolveMove(ContainerMatrix.InventoryPage0, 0, 0, ContainerMatrix.InventoryPage0,
            1, source, null, false);
        var projected = ContainerMatrix.ApplyMove(move, ContainerMatrix.InventoryPage0, 0, current,
            ContainerMatrix.InventoryPage0, 1, current);

        Assert.False(projected.From.ContainsKey(0));
        Assert.Equal(source, projected.From[1]);
        Assert.Same(projected.From, projected.To);
    }

    [Fact]
    public void ApplyMove_CrossContainer_EachSidePreservesItsOtherSlots()
    {
        var moved = Stack(1);
        var untouchedInInventory = Stack(2);
        var fromCurrent = ImmutableDictionary<byte, ItemStack>.Empty
            .Add(0, moved)
            .Add(5, untouchedInInventory);
        var toCurrent = ImmutableDictionary<byte, ItemStack>.Empty;

        var move = ContainerMatrix.ResolveMove(ContainerMatrix.InventoryPage0, 0, 0, ContainerMatrix.Equipment, 7,
            moved, null, false);
        var projected = ContainerMatrix.ApplyMove(move, ContainerMatrix.InventoryPage0, 0, fromCurrent,
            ContainerMatrix.Equipment, 7, toCurrent);

        Assert.False(projected.From.ContainsKey(0));
        Assert.Equal(untouchedInInventory, projected.From[5]);
        Assert.Equal(moved, projected.To[7]);
    }
}
