using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Mounts;

namespace Fenrir.Application.Game.Tests.Mounts;

public class MountStateResolverTests
{
    private static readonly ImmutableArray<int> EmptyGarage =
        ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static readonly ImmutableArray<int> EmptyPerSlotCounters =
        ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static MountStateResolver.Context Ctx(int animalIndex, int animalTime, int actionSort,
        ImmutableArray<int>? garage = null, ImmutableArray<int>? accumulatedExp = null,
        ImmutableArray<int>? rolledAttributeTotal = null, bool hasAttributeDeleteMaterial = false,
        bool hasAttributeTransferMaterial = false)
    {
        return new MountStateResolver.Context(animalIndex, animalTime, actionSort, garage ?? EmptyGarage,
            accumulatedExp ?? EmptyPerSlotCounters, rolledAttributeTotal ?? EmptyPerSlotCounters,
            hasAttributeDeleteMaterial, hasAttributeTransferMaterial);
    }

    [Fact]
    public void Select_ValidSlot_Succeeds()
    {
        var ctx = Ctx(-1, 0, 0);
        var result = MountStateResolver.Resolve(1, 3, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Select, result.Kind);
        Assert.Equal(3, result.NewAnimalIndex);
    }

    [Fact]
    public void Select_SlotOutOfRange_NoReply()
    {
        var ctx = Ctx(-1, 0, 0);
        var result = MountStateResolver.Resolve(1, 10, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Deselect_WhileMounted_NoReply()
    {
        var ctx = Ctx(12, 0, 0);
        var result = MountStateResolver.Resolve(2, 2, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Deselect_WhileNotMounted_Succeeds()
    {
        var ctx = Ctx(2, 0, 0);
        var result = MountStateResolver.Resolve(2, 2, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Deselect, result.Kind);
        Assert.Equal(-1, result.NewAnimalIndex);
    }

    [Fact]
    public void Mount_AnimalTimeZero_NoReply()
    {
        var ctx = Ctx(3, 0, 1);
        var result = MountStateResolver.Resolve(3, 0, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Mount_NotIdle_NoReply()
    {
        var ctx = Ctx(3, 5, 0);
        var result = MountStateResolver.Resolve(3, 0, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Mount_ValidPreconditions_Succeeds_AndReadsGarageSlot()
    {
        var garage = EmptyGarage.SetItem(3, 1006);
        var ctx = Ctx(3, 5, 1, garage);
        var result = MountStateResolver.Resolve(3, 0, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Mount, result.Kind);
        Assert.Equal(13, result.NewAnimalIndex);
        Assert.Equal(1006, result.NewAnimalNumber);
    }

    [Fact]
    public void Dismount_NotCurrentlyMounted_NoReply()
    {
        var ctx = Ctx(3, 0, 0);
        var result = MountStateResolver.Resolve(4, 0, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Dismount_CurrentlyMounted_Succeeds()
    {
        var ctx = Ctx(13, 0, 0);
        var result = MountStateResolver.Resolve(4, 0, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Dismount, result.Kind);
        Assert.Equal(3, result.NewAnimalIndex);
    }

    [Fact]
    public void DeleteMount_SelectionOutOfRange_Disconnects()
    {
        var garage = EmptyGarage.SetItem(2, 1006);
        var ctx = Ctx(12, 0, 0, garage);
        var result = MountStateResolver.Resolve(5, 1006, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Disconnect, result.Kind);
    }

    [Fact]
    public void DeleteMount_ValueZero_NeverMatchesEmptySlot_Disconnects()
    {
        var ctx = Ctx(0, 0, 0);
        var result = MountStateResolver.Resolve(5, 0, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Disconnect, result.Kind);
    }

    [Fact]
    public void DeleteMount_ValueMatchesOwnedSlot_Succeeds()
    {
        var garage = EmptyGarage.SetItem(2, 1006);
        var ctx = Ctx(0, 0, 0, garage);
        var result = MountStateResolver.Resolve(5, 1006, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.DeleteMount, result.Kind);
        Assert.Equal(2, result.GarageSlot);
    }

    [Fact]
    public void DeleteMount_ValueDoesNotMatchAnySlot_Disconnects()
    {
        var ctx = Ctx(0, 0, 0);
        var result = MountStateResolver.Resolve(5, 4242, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Disconnect, result.Kind);
    }

    [Fact]
    public void ConvertAttribute_AccumulatedExpBelowThreshold_Disconnects()
    {
        var ctx = Ctx(3, 0, 0);
        var result = MountStateResolver.Resolve(6, 0, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Disconnect, result.Kind);
    }

    [Fact]
    public void ConvertAttribute_AtExpThresholdAndBelowCap_StillDisconnects_NoCatalogedRoll()
    {
        var exp = EmptyPerSlotCounters.SetItem(3, MountStateResolver.MaxMountExp);
        var ctx = Ctx(3, 0, 0, accumulatedExp: exp);
        var result = MountStateResolver.Resolve(6, 0, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Disconnect, result.Kind);
    }

    [Fact]
    public void ConvertAttribute_TotalAtCap_Disconnects()
    {
        var exp = EmptyPerSlotCounters.SetItem(3, MountStateResolver.MaxMountExp);
        var total = EmptyPerSlotCounters.SetItem(3, MountStateResolver.MaxRolledAttributeTotal);
        var ctx = Ctx(3, 0, 0, accumulatedExp: exp, rolledAttributeTotal: total);
        var result = MountStateResolver.Resolve(6, 0, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Disconnect, result.Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void DeleteAttribute_StatSlotOutOfRange_Disconnects(int statSlot)
    {
        var ctx = Ctx(3, 0, 0, hasAttributeDeleteMaterial: true);
        var result = MountStateResolver.Resolve(7, statSlot, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Disconnect, result.Kind);
    }

    [Fact]
    public void DeleteAttribute_MaterialMissing_Disconnects()
    {
        var ctx = Ctx(3, 0, 0, hasAttributeDeleteMaterial: false);
        var result = MountStateResolver.Resolve(7, 1, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Disconnect, result.Kind);
    }

    [Fact]
    public void DeleteAttribute_ValidRequest_Succeeds()
    {
        var ctx = Ctx(13, 0, 0, hasAttributeDeleteMaterial: true);
        var result = MountStateResolver.Resolve(7, 5, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.DeleteAttribute, result.Kind);
        Assert.Equal(3, result.GarageSlot);
        Assert.Equal(4, result.StatSlotIndex);
    }

    [Fact]
    public void TransferAttribute_SelectionOutOfRange_SilentNoReply()
    {
        var ctx = Ctx(-1, 0, 0, hasAttributeTransferMaterial: true);
        var result = MountStateResolver.Resolve(8, 3, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void TransferAttribute_StatSlotOutOfRange_Disconnects(int statSlot)
    {
        var ctx = Ctx(3, 0, 0, hasAttributeTransferMaterial: true);
        var result = MountStateResolver.Resolve(8, statSlot, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Disconnect, result.Kind);
    }

    [Fact]
    public void TransferAttribute_MaterialMissing_Disconnects()
    {
        var ctx = Ctx(3, 0, 0, hasAttributeTransferMaterial: false);
        var result = MountStateResolver.Resolve(8, 3, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Disconnect, result.Kind);
    }

    [Fact]
    public void TransferAttribute_GatesPass_StillDisconnects_NoCatalogedTransferMechanic()
    {
        var ctx = Ctx(3, 0, 0, hasAttributeTransferMaterial: true);
        var result = MountStateResolver.Resolve(8, 3, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Disconnect, result.Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(120)]
    public void UnsupportedSort_Disconnects(int sort)
    {
        var ctx = Ctx(-1, 0, 0);
        var result = MountStateResolver.Resolve(sort, 0, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Disconnect, result.Kind);
    }
}
