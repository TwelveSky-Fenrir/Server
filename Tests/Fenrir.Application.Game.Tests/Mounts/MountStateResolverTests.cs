using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Mounts;

namespace Fenrir.Application.Game.Tests.Mounts;

public class MountStateResolverTests
{
    private static readonly ImmutableArray<int> EmptyGarage =
        ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void Select_ValidSlot_Succeeds()
    {
        var ctx = new MountStateResolver.Context(-1, 0, 0, EmptyGarage);
        var result = MountStateResolver.Resolve(1, 3, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Select, result.Kind);
        Assert.Equal(3, result.NewAnimalIndex);
    }

    [Fact]
    public void Select_SlotOutOfRange_NoReply()
    {
        var ctx = new MountStateResolver.Context(-1, 0, 0, EmptyGarage);
        var result = MountStateResolver.Resolve(1, 10, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Deselect_WhileMounted_NoReply()
    {
        var ctx = new MountStateResolver.Context(12, 0, 0, EmptyGarage);
        var result = MountStateResolver.Resolve(2, 2, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Deselect_WhileNotMounted_Succeeds()
    {
        var ctx = new MountStateResolver.Context(2, 0, 0, EmptyGarage);
        var result = MountStateResolver.Resolve(2, 2, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Deselect, result.Kind);
        Assert.Equal(-1, result.NewAnimalIndex);
    }

    [Fact]
    public void Mount_AnimalTimeZero_NoReply()
    {
        var ctx = new MountStateResolver.Context(3, 0, 1, EmptyGarage);
        var result = MountStateResolver.Resolve(3, 0, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Mount_NotIdle_NoReply()
    {
        var ctx = new MountStateResolver.Context(3, 5, 0, EmptyGarage);
        var result = MountStateResolver.Resolve(3, 0, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Mount_ValidPreconditions_Succeeds_AndReadsGarageSlot()
    {
        var garage = EmptyGarage.SetItem(3, 1006);
        var ctx = new MountStateResolver.Context(3, 5, 1, garage);
        var result = MountStateResolver.Resolve(3, 0, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Mount, result.Kind);
        Assert.Equal(13, result.NewAnimalIndex);
        Assert.Equal(1006, result.NewAnimalNumber);
    }

    [Fact]
    public void Dismount_NotCurrentlyMounted_NoReply()
    {
        var ctx = new MountStateResolver.Context(3, 0, 0, EmptyGarage);
        var result = MountStateResolver.Resolve(4, 0, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Dismount_CurrentlyMounted_Succeeds()
    {
        var ctx = new MountStateResolver.Context(13, 0, 0, EmptyGarage);
        var result = MountStateResolver.Resolve(4, 0, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Dismount, result.Kind);
        Assert.Equal(3, result.NewAnimalIndex);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(120)]
    public void UnsupportedSort_Disconnects(int sort)
    {
        var ctx = new MountStateResolver.Context(-1, 0, 0, EmptyGarage);
        var result = MountStateResolver.Resolve(sort, 0, in ctx);

        Assert.Equal(MountStateResolver.ResultKind.Disconnect, result.Kind);
    }
}
