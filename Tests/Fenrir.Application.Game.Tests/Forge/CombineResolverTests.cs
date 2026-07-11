using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Forge;

public class CombineResolverTests
{
    private static ItemRowDto TargetItem(byte type = CombineResolver.RareItemType, byte sort = 7,
        byte checkHighImprove = 2, short level = 100, byte martialLevel = 0, byte checkSetItem = 0)
    {
        return WorldDataTestRows.Item(1) with
        {
            Type = type, Sort = sort, CheckHighImprove = checkHighImprove, Level = level,
            MartialLevel = martialLevel, CheckSetItem = checkSetItem
        };
    }

    private static ItemStack TargetStack(byte combine, byte enchant = 0)
    {
        return new ItemStack(1, 1, enchant, combine, 0, 0, 0, 0, 0, 0, 111);
    }

    private static ItemRowDto ScrollMaterial(int itemId)
    {
        return WorldDataTestRows.Item(itemId);
    }

    private static ItemStack MaterialStack(int itemId)
    {
        return new ItemStack(itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 222);
    }

    [Fact]
    public void TargetTypeNormal_IsRejected()
    {
        var target = TargetItem(0);
        var result = CombineResolver.Resolve(target, TargetStack(0), ScrollMaterial(2001), MaterialStack(2001), 0, 0,
            new ScriptedRandomSource(0));

        Assert.True(result.IsRejected);
    }

    [Fact]
    public void TargetSortOutOfRange_IsRejected()
    {
        var target = TargetItem(sort: 6);
        var result = CombineResolver.Resolve(target, TargetStack(0), ScrollMaterial(2001), MaterialStack(2001), 0, 0,
            new ScriptedRandomSource(0));

        Assert.True(result.IsRejected);
    }

    [Fact]
    public void TargetCheckHighImproveNotTwo_IsRejected()
    {
        var target = TargetItem(checkHighImprove: 0);
        var result = CombineResolver.Resolve(target, TargetStack(0), ScrollMaterial(2001), MaterialStack(2001), 0, 0,
            new ScriptedRandomSource(0));

        Assert.True(result.IsRejected);
    }

    [Fact]
    public void TargetCombineAtMax_IsRejected()
    {
        var target = TargetItem();
        var result = CombineResolver.Resolve(target, TargetStack(CombineResolver.MaxCombine), ScrollMaterial(2001),
            MaterialStack(2001), 0, 0, new ScriptedRandomSource(0));

        Assert.True(result.IsRejected);
    }

    [Theory]
    [InlineData(2001, 49)]
    [InlineData(2002, 79)]
    [InlineData(2003, 99)]
    public void ScrollMaterial_RollBelowFixedProbability_Succeeds(int scrollId, int roll)
    {
        var target = TargetItem();
        var result = CombineResolver.Resolve(target, TargetStack(0), ScrollMaterial(scrollId),
            MaterialStack(scrollId), 0, 0, new ScriptedRandomSource(roll));

        Assert.Equal(0, result.ResultCode);
        Assert.Equal(1, result.NewCombine);
        Assert.True(result.MaterialConsumed);
    }

    [Fact]
    public void ScrollMaterial_2001_RollAtOrAboveFiftyPercent_FailsButStillConsumesMaterial()
    {
        var target = TargetItem();
        var result = CombineResolver.Resolve(target, TargetStack(3), ScrollMaterial(2001), MaterialStack(2001), 0, 0,
            new ScriptedRandomSource(50));

        Assert.Equal(3, result.ResultCode);
        Assert.Equal(3, result.NewCombine);
        Assert.True(result.MaterialConsumed);
    }

    [Fact]
    public void ScrollMaterial_2003_AlwaysSucceeds()
    {
        var target = TargetItem();
        var result = CombineResolver.Resolve(target, TargetStack(0), ScrollMaterial(2003), MaterialStack(2003), 0, 0,
            new ScriptedRandomSource(99));

        Assert.Equal(0, result.ResultCode);
    }

    private static ItemRowDto NormalMaterial(int itemId = 2, byte type = CombineResolver.RareItemType,
        byte sort = 7, byte checkHighImprove = 2, short level = 100, byte martialLevel = 0, byte checkSetItem = 0)
    {
        return WorldDataTestRows.Item(itemId) with
        {
            Type = type, Sort = sort, CheckHighImprove = checkHighImprove, Level = level,
            MartialLevel = martialLevel, CheckSetItem = checkSetItem
        };
    }

    [Fact]
    public void NormalMaterial_Success_IncrementsCombineAndConsumesMaterial()
    {
        var target = TargetItem(level: 100, martialLevel: 0);
        var material = NormalMaterial(level: 100, martialLevel: 0);
        var result = CombineResolver.Resolve(target, TargetStack(0), material, MaterialStack(2), 0, 0,
            new ScriptedRandomSource(10));

        Assert.Equal(0, result.ResultCode);
        Assert.Equal(1, result.NewCombine);
        Assert.True(result.MaterialConsumed);
    }

    [Fact]
    public void NormalMaterial_Failure_MaterialSurvives()
    {
        var target = TargetItem(level: 100, martialLevel: 0);
        var material = NormalMaterial(level: 100, martialLevel: 0);
        var result = CombineResolver.Resolve(target, TargetStack(0), material, MaterialStack(2), 0, 0,
            new ScriptedRandomSource(90));

        Assert.Equal(1, result.ResultCode);
        Assert.Equal(0, result.NewCombine);
        Assert.False(result.MaterialConsumed);
    }

    [Fact]
    public void NormalMaterial_TypeMismatch_IsRejected()
    {
        var target = TargetItem();
        var material = NormalMaterial(type: 0);
        var result = CombineResolver.Resolve(target, TargetStack(0), material, MaterialStack(2), 0, 0,
            new ScriptedRandomSource(0));

        Assert.True(result.IsRejected);
    }

    [Fact]
    public void NormalMaterial_AlreadyEnchanted_NonSetItem_IsRejected()
    {
        var target = TargetItem(level: 100);
        var material = NormalMaterial(level: 100);
        var enchantedMaterialStack = MaterialStack(2) with { Enchant = 1 };
        var result = CombineResolver.Resolve(target, TargetStack(0), material, enchantedMaterialStack, 0, 0,
            new ScriptedRandomSource(0));

        Assert.True(result.IsRejected);
    }

    [Fact]
    public void NormalMaterial_RareLevelMismatch_IsRejected()
    {
        var target = TargetItem(level: 100);
        var material = NormalMaterial(level: 95);
        var result = CombineResolver.Resolve(target, TargetStack(0), material, MaterialStack(2), 0, 0,
            new ScriptedRandomSource(0));

        Assert.True(result.IsRejected);
    }

    [Fact]
    public void NormalMaterial_RareLevelMatchIncludingMartial_IsAccepted()
    {
        var target = TargetItem(level: 100, martialLevel: 2);
        var material = NormalMaterial(level: 98, martialLevel: 4);
        var result = CombineResolver.Resolve(target, TargetStack(0), material, MaterialStack(2), 0, 0,
            new ScriptedRandomSource(10));

        Assert.False(result.IsRejected);
    }

    [Fact]
    public void EliteTierMismatch_IsRejected()
    {
        var target = TargetItem(CombineResolver.EliteItemType, level: 100);
        var material = NormalMaterial(type: CombineResolver.EliteItemType, level: 96);
        var result = CombineResolver.Resolve(target, TargetStack(0), material, MaterialStack(2), 0, 0,
            new ScriptedRandomSource(0));

        Assert.True(result.IsRejected);
    }

    [Fact]
    public void EliteTierMatch_IsAccepted()
    {
        var target = TargetItem(CombineResolver.EliteItemType, level: 100);
        var material = NormalMaterial(type: CombineResolver.EliteItemType, level: 95);
        var result = CombineResolver.Resolve(target, TargetStack(0), material, MaterialStack(2), 0, 0,
            new ScriptedRandomSource(10));

        Assert.False(result.IsRejected);
    }

    [Fact]
    public void SetItemTarget_RequiresSetMaterialSameType()
    {
        var target = TargetItem(checkSetItem: 2, level: 100);
        var material = NormalMaterial(checkSetItem: 0, level: 100);
        var result = CombineResolver.Resolve(target, TargetStack(0, 44), material, MaterialStack(2), 0, 0,
            new ScriptedRandomSource(0));

        Assert.True(result.IsRejected);
    }

    [Fact]
    public void SetItemTarget_BelowRequiredEnchant_IsRejected()
    {
        var target = TargetItem(checkSetItem: 2, level: 100);
        var material = NormalMaterial(checkSetItem: 2, level: 100);
        var result = CombineResolver.Resolve(target, TargetStack(0, 43), material, MaterialStack(2), 0, 0,
            new ScriptedRandomSource(0));

        Assert.True(result.IsRejected);
    }

    [Fact]
    public void LuckyCharge_AddsFivePercentAndIsConsumed()
    {
        var target = TargetItem(level: 100);
        var material = NormalMaterial(level: 100);
        var result = CombineResolver.Resolve(target, TargetStack(0), material, MaterialStack(2), 0, 1,
            new ScriptedRandomSource(68));

        Assert.Equal(0, result.ResultCode);
        Assert.True(result.ConsumesLuckyCharge);
    }
}
