using System.Collections.Immutable;
using Fenrir.Application.Game.Runes;

namespace Fenrir.Application.Game.Tests.Runes;

public class RuneSocketResolverTests
{
    private static readonly ImmutableArray<int> Empty = ImmutableArray.Create(0, 0, 0, 0);

    [Fact]
    public void ResolveInsert_MatchingSlotEmpty_Succeeds()
    {
        var result = RuneSocketResolver.ResolveInsert(0, 93514, Empty);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ResolveInsert_ItemIdOutOfFamily_Rejected()
    {
        var result = RuneSocketResolver.ResolveInsert(0, 12345, Empty);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveInsert_RuneIndexOutOfRange_Rejected()
    {
        var result = RuneSocketResolver.ResolveInsert(4, 93514, Empty);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveInsert_TargetSlotAlreadyOccupied_Rejected()
    {
        var runeSystem = Empty.SetItem(2, 93516);

        var result = RuneSocketResolver.ResolveInsert(2, 93514, runeSystem);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveInsert_NaturalSlotAlreadyOccupied_RejectedEvenIfTargetSlotDiffers()
    {
        // Item 93514's natural slot (0) is already occupied; inserting at a different client-supplied
        // slot (2) must still be rejected -- mirrors the legacy's own redundant per-item-id safety check.
        var runeSystem = Empty.SetItem(0, 93514);

        var result = RuneSocketResolver.ResolveInsert(2, 93514, runeSystem);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveRemove_MatchingOccupant_Succeeds()
    {
        var runeSystem = Empty.SetItem(1, 93515);

        var result = RuneSocketResolver.ResolveRemove(1, runeSystem, true);

        Assert.True(result.Succeeded);
        Assert.Equal(93515, result.ItemId);
    }

    [Fact]
    public void ResolveRemove_EmptySlot_Rejected()
    {
        var result = RuneSocketResolver.ResolveRemove(1, Empty, true);

        Assert.Equal(RuneSocketResolver.RemoveOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void ResolveRemove_MismatchedOccupant_Rejected()
    {
        // Slot 1 should only ever hold 93515 -- a mismatched occupant (defensive/UB-adjacent state) is rejected.
        var runeSystem = Empty.SetItem(1, 93514);

        var result = RuneSocketResolver.ResolveRemove(1, runeSystem, true);

        Assert.Equal(RuneSocketResolver.RemoveOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void ResolveRemove_NoFreeInventorySlot_InventoryFullNotRejected()
    {
        var runeSystem = Empty.SetItem(3, 93517);

        var result = RuneSocketResolver.ResolveRemove(3, runeSystem, false);

        Assert.Equal(RuneSocketResolver.RemoveOutcome.InventoryFull, result.Outcome);
    }

    [Fact]
    public void ResolveRemove_RuneIndexOutOfRange_Rejected()
    {
        var result = RuneSocketResolver.ResolveRemove(-1, Empty, true);

        Assert.Equal(RuneSocketResolver.RemoveOutcome.Rejected, result.Outcome);
    }
}
