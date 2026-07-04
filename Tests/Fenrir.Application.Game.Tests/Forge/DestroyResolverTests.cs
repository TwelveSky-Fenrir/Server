using Fenrir.Application.Game.Forge;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.Forge;

public class DestroyResolverTests
{
    private static ItemStack Target(byte enchant)
    {
        return new ItemStack(1, 1, enchant, 0, 0, 0, 0, 0, 0, 0, 777);
    }

    [Fact]
    public void EnchantZero_IsRejected()
    {
        var item = WorldDataTestRows.Item(1) with { Type = DestroyResolver.RareItemType, Sort = 7, CheckImprove = 2 };
        var result = DestroyResolver.Resolve(item, Target(0));

        Assert.Equal(DestroyResolver.DestroyOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void EliteType_IsRejected()
    {
        var item = WorldDataTestRows.Item(1) with { Type = 4, Sort = 7, CheckImprove = 2 };
        var result = DestroyResolver.Resolve(item, Target(10));

        Assert.Equal(DestroyResolver.DestroyOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void SortOutOfEquipRange_IsRejected()
    {
        var item = WorldDataTestRows.Item(1) with
        {
            Type = DestroyResolver.RareItemType, Sort = 6, CheckImprove = 2
        };
        var result = DestroyResolver.Resolve(item, Target(10));

        Assert.Equal(DestroyResolver.DestroyOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void CheckImproveNotTwo_IsRejected()
    {
        var item = WorldDataTestRows.Item(1) with
        {
            Type = DestroyResolver.RareItemType, Sort = 7, CheckImprove = 0
        };
        var result = DestroyResolver.Resolve(item, Target(10));

        Assert.Equal(DestroyResolver.DestroyOutcome.Rejected, result.Outcome);
    }

    [Theory]
    [InlineData(1, 1_000_000, 1021)]
    [InlineData(10, 1_000_000, 1021)]
    [InlineData(11, 2_000_000, 1021)]
    [InlineData(20, 2_000_000, 1021)]
    [InlineData(21, 3_000_000, 1022)]
    [InlineData(30, 3_000_000, 1022)]
    [InlineData(31, 5_000_000, 1023)]
    [InlineData(39, 5_000_000, 1023)]
    [InlineData(40, 10_000_000, 1437)]
    [InlineData(50, 10_000_000, 1437)]
    public void EnchantTier_MapsToExpectedMoneyAndStone(byte enchant, int expectedMoney, int expectedStone)
    {
        var item = WorldDataTestRows.Item(1) with
        {
            Type = DestroyResolver.RareItemType, Sort = 7, CheckImprove = 2
        };
        var result = DestroyResolver.Resolve(item, Target(enchant));

        Assert.Equal(DestroyResolver.DestroyOutcome.Success, result.Outcome);
        Assert.Equal(expectedMoney, result.Money);
        Assert.Equal(expectedStone, result.StoneItemId);
    }
}
