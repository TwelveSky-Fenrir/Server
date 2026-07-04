using System.Collections.Immutable;
using Fenrir.Application.Game.StellarCores;

namespace Fenrir.Application.Game.Tests.StellarCores;

public class StellarCoreStateResolverTests
{
    private static readonly ImmutableArray<int> EmptyWardrobe =
        ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void Select_EmptySlot_NoReply()
    {
        var ctx = new StellarCoreStateResolver.Context(-1, EmptyWardrobe);
        var result = StellarCoreStateResolver.Resolve(1, 2, in ctx);

        Assert.Equal(StellarCoreStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Select_OccupiedSlot_Succeeds()
    {
        var wardrobe = EmptyWardrobe.SetItem(2, 76527);
        var ctx = new StellarCoreStateResolver.Context(-1, wardrobe);
        var result = StellarCoreStateResolver.Resolve(1, 2, in ctx);

        Assert.Equal(StellarCoreStateResolver.ResultKind.Select, result.Kind);
        Assert.Equal(2, result.NewCoreIndex);
    }

    [Fact]
    public void NoOpSort_AlwaysNoReply()
    {
        var ctx = new StellarCoreStateResolver.Context(-1, EmptyWardrobe);
        var result = StellarCoreStateResolver.Resolve(2, 0, in ctx);

        Assert.Equal(StellarCoreStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Equip_NegativeIndexDefault_NoReply_NotOutOfRangeCrash()
    {
        // Legacy UB: aStellarCoreIndex == -1 indexes the array with a negative subscript. Fixed per D8.
        var ctx = new StellarCoreStateResolver.Context(-1, EmptyWardrobe);
        var result = StellarCoreStateResolver.Resolve(3, 0, in ctx);

        Assert.Equal(StellarCoreStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Equip_SelectedOccupiedSlot_Succeeds()
    {
        var wardrobe = EmptyWardrobe.SetItem(4, 76528);
        var ctx = new StellarCoreStateResolver.Context(4, wardrobe);
        var result = StellarCoreStateResolver.Resolve(3, 0, in ctx);

        Assert.Equal(StellarCoreStateResolver.ResultKind.Equip, result.Kind);
        Assert.Equal(14, result.NewCoreIndex);
        Assert.Equal(76528, result.NewCoreNumber);
    }

    [Fact]
    public void Remove_NotWorn_NoReply()
    {
        var ctx = new StellarCoreStateResolver.Context(4, EmptyWardrobe);
        var result = StellarCoreStateResolver.Resolve(4, 0, in ctx);

        Assert.Equal(StellarCoreStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Remove_Worn_Succeeds()
    {
        var wardrobe = EmptyWardrobe.SetItem(4, 76528);
        var ctx = new StellarCoreStateResolver.Context(14, wardrobe);
        var result = StellarCoreStateResolver.Resolve(4, 0, in ctx);

        Assert.Equal(StellarCoreStateResolver.ResultKind.Remove, result.Kind);
        Assert.Equal(4, result.NewCoreIndex);
    }

    [Fact]
    public void ReturnToInventory_IndexMismatch_RepliesMismatchNotDisconnect()
    {
        var ctx = new StellarCoreStateResolver.Context(-1, EmptyWardrobe);
        var result = StellarCoreStateResolver.Resolve(5, 3, in ctx);

        Assert.Equal(StellarCoreStateResolver.ResultKind.ReturnToInventoryMismatch, result.Kind);
    }

    [Fact]
    public void ReturnToInventory_MatchingOccupiedSlot_Succeeds()
    {
        var wardrobe = EmptyWardrobe.SetItem(3, 76529);
        var ctx = new StellarCoreStateResolver.Context(3, wardrobe);
        var result = StellarCoreStateResolver.Resolve(5, 3, in ctx);

        Assert.Equal(StellarCoreStateResolver.ResultKind.ReturnToInventorySuccess, result.Kind);
        Assert.Equal(-1, result.NewCoreIndex);
        Assert.Equal(76529, result.GrantedItemId);
        Assert.Equal(3, result.ClearedSlot);
    }

    [Fact]
    public void UnsupportedSort_Disconnects()
    {
        var ctx = new StellarCoreStateResolver.Context(-1, EmptyWardrobe);
        var result = StellarCoreStateResolver.Resolve(9, 0, in ctx);

        Assert.Equal(StellarCoreStateResolver.ResultKind.Disconnect, result.Kind);
    }
}
