using System.Collections.Immutable;
using Fenrir.Application.Game.Costumes;

namespace Fenrir.Application.Game.Tests.Costumes;

public class CostumeStateResolverTests
{
    private static readonly ImmutableArray<int> EmptyWardrobe =
        ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void Select_EmptySlot_NoReply()
    {
        var ctx = new CostumeStateResolver.Context(-1, EmptyWardrobe);
        var result = CostumeStateResolver.Resolve(1, 2, in ctx);

        Assert.Equal(CostumeStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Select_OccupiedSlot_Succeeds()
    {
        var wardrobe = EmptyWardrobe.SetItem(2, 301);
        var ctx = new CostumeStateResolver.Context(-1, wardrobe);
        var result = CostumeStateResolver.Resolve(1, 2, in ctx);

        Assert.Equal(CostumeStateResolver.ResultKind.Select, result.Kind);
        Assert.Equal(2, result.NewCostumeIndex);
    }

    [Fact]
    public void NoOpSort_AlwaysNoReply()
    {
        var ctx = new CostumeStateResolver.Context(-1, EmptyWardrobe);
        var result = CostumeStateResolver.Resolve(2, 0, in ctx);

        Assert.Equal(CostumeStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Equip_UnselectedSlot_NoReply()
    {
        var ctx = new CostumeStateResolver.Context(-1, EmptyWardrobe);
        var result = CostumeStateResolver.Resolve(3, 0, in ctx);

        Assert.Equal(CostumeStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Equip_SelectedOccupiedSlot_Succeeds()
    {
        var wardrobe = EmptyWardrobe.SetItem(4, 305);
        var ctx = new CostumeStateResolver.Context(4, wardrobe);
        var result = CostumeStateResolver.Resolve(3, 0, in ctx);

        Assert.Equal(CostumeStateResolver.ResultKind.Equip, result.Kind);
        Assert.Equal(14, result.NewCostumeIndex);
        Assert.Equal(305, result.NewCostumeNumber);
    }

    [Fact]
    public void Remove_NotWorn_NoReply()
    {
        var ctx = new CostumeStateResolver.Context(4, EmptyWardrobe);
        var result = CostumeStateResolver.Resolve(4, 0, in ctx);

        Assert.Equal(CostumeStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Remove_Worn_Succeeds()
    {
        var wardrobe = EmptyWardrobe.SetItem(4, 305);
        var ctx = new CostumeStateResolver.Context(14, wardrobe);
        var result = CostumeStateResolver.Resolve(4, 0, in ctx);

        Assert.Equal(CostumeStateResolver.ResultKind.Remove, result.Kind);
        Assert.Equal(4, result.NewCostumeIndex);
    }

    [Fact]
    public void ReturnToInventory_IndexMismatch_RepliesMismatchNotDisconnect()
    {
        var ctx = new CostumeStateResolver.Context(-1, EmptyWardrobe);
        var result = CostumeStateResolver.Resolve(5, 3, in ctx);

        Assert.Equal(CostumeStateResolver.ResultKind.ReturnToInventoryMismatch, result.Kind);
    }

    [Fact]
    public void ReturnToInventory_MatchingButEmptySlot_Disconnects()
    {
        var ctx = new CostumeStateResolver.Context(3, EmptyWardrobe);
        var result = CostumeStateResolver.Resolve(5, 3, in ctx);

        Assert.Equal(CostumeStateResolver.ResultKind.Disconnect, result.Kind);
    }

    [Fact]
    public void ReturnToInventory_MatchingOccupiedSlot_Succeeds()
    {
        var wardrobe = EmptyWardrobe.SetItem(3, 305);
        var ctx = new CostumeStateResolver.Context(3, wardrobe);
        var result = CostumeStateResolver.Resolve(5, 3, in ctx);

        Assert.Equal(CostumeStateResolver.ResultKind.ReturnToInventorySuccess, result.Kind);
        Assert.Equal(-1, result.NewCostumeIndex);
        Assert.Equal(305, result.GrantedItemId);
        Assert.Equal(3, result.ClearedSlot);
    }

    [Fact]
    public void UnsupportedSort_Disconnects()
    {
        var ctx = new CostumeStateResolver.Context(-1, EmptyWardrobe);
        var result = CostumeStateResolver.Resolve(9, 0, in ctx);

        Assert.Equal(CostumeStateResolver.ResultKind.Disconnect, result.Kind);
    }
}
