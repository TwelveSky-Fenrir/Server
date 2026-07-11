using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.Enchant;

public class StellarCoreResolverTests
{
    private static ItemDefinition Def(int itemId)
    {
        return new ItemDefinition(WorldDataTestRows.Item(itemId), []);
    }

    [Fact]
    public void MaterialNotIdenticalToTarget_IsRejected()
    {
        var result = StellarCoreResolver.Resolve(Def(90000), Def(90001), false);

        Assert.Equal(StellarCoreResolver.StellarCoreOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void TargetAtOrAboveCeiling_IsRejected()
    {
        var atCeiling = StellarCoreResolver.MaxStellarCoreItemIdExclusive;
        var result = StellarCoreResolver.Resolve(Def(atCeiling), Def(atCeiling), false);

        Assert.Equal(StellarCoreResolver.StellarCoreOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void ValidMerge_IncrementsItemIdAndClearsMaterial()
    {
        var result = StellarCoreResolver.Resolve(Def(90000), Def(90000), false);

        Assert.Equal(StellarCoreResolver.StellarCoreOutcome.Merged, result.Outcome);
        Assert.Equal(90001, result.NewTargetItemId);
        Assert.True(result.ClearsMaterialSlot);
    }

    [Fact]
    public void ValidMerge_NonPremium_CostsFullBasePrice()
    {
        var result = StellarCoreResolver.Resolve(Def(90000), Def(90000), false);

        Assert.Equal(StellarCoreResolver.BaseMergeCost, result.Cost);
    }

    [Fact]
    public void ValidMerge_Premium_Applies20PercentDiscount()
    {
        var result = StellarCoreResolver.Resolve(Def(90000), Def(90000), true);

        Assert.Equal(40_000_000, result.Cost);
    }

    [Fact]
    public void OneBelowCeiling_IsStillMergeable()
    {
        var justBelow = StellarCoreResolver.MaxStellarCoreItemIdExclusive - 1;
        var result = StellarCoreResolver.Resolve(Def(justBelow), Def(justBelow), false);

        Assert.Equal(StellarCoreResolver.StellarCoreOutcome.Merged, result.Outcome);
        Assert.Equal(StellarCoreResolver.MaxStellarCoreItemIdExclusive, result.NewTargetItemId);
    }
}
