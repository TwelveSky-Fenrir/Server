using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Enchant;

public class SkyUpgradeResolverTests
{
    private static ItemRowDto WarlordItem(int itemId = 87000, byte sort = 7, byte checkSetItem = 2)
    {
        return WorldDataTestRows.Item(itemId) with { Sort = sort, CheckSetItem = checkSetItem };
    }

    [Fact]
    public void EquipSortOutOfRange_Rejected()
    {
        var item = WarlordItem(sort: 5);

        var result = SkyUpgradeResolver.Resolve(item, 30, 501, new ScriptedRandomSource(0));

        Assert.Equal(SkyUpgradeResolver.Outcome.Rejected, result.Outcome);
    }

    [Fact]
    public void CheckSetItemNotTwo_Rejected()
    {
        var item = WarlordItem(checkSetItem: 0);

        var result = SkyUpgradeResolver.Resolve(item, 30, 501, new ScriptedRandomSource(0));

        Assert.Equal(SkyUpgradeResolver.Outcome.Rejected, result.Outcome);
    }

    [Fact]
    public void ItemIdOutOfWarlordRange_Rejected()
    {
        var item = WarlordItem(500);

        var result = SkyUpgradeResolver.Resolve(item, 30, 501, new ScriptedRandomSource(0));

        Assert.Equal(SkyUpgradeResolver.Outcome.Rejected, result.Outcome);
    }

    [Fact]
    public void EnchantBelowThirty_Rejected()
    {
        var item = WarlordItem();

        var result = SkyUpgradeResolver.Resolve(item, 29, 501, new ScriptedRandomSource(0));

        Assert.Equal(SkyUpgradeResolver.Outcome.Rejected, result.Outcome);
    }

    [Fact]
    public void UnrecognizedMaterial_Rejected()
    {
        var item = WarlordItem();

        var result = SkyUpgradeResolver.Resolve(item, 30, 999, new ScriptedRandomSource(0));

        Assert.Equal(SkyUpgradeResolver.Outcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Material501_SuccessRoll_ShiftsItemIdAndDecreasesEnchant()
    {
        var item = WarlordItem();

        var result = SkyUpgradeResolver.Resolve(item, 30, 501, new ScriptedRandomSource(0));

        Assert.True(result.Succeeded);
        Assert.Equal(87129, result.NewItemId);
        Assert.Equal(22, result.NewEnchant);
    }

    [Fact]
    public void Material501_FailureRoll_NoItemMutation()
    {
        var item = WarlordItem();

        var result = SkyUpgradeResolver.Resolve(item, 30, 501, new ScriptedRandomSource(40));

        Assert.Equal(SkyUpgradeResolver.Outcome.Failed, result.Outcome);
    }

    [Fact]
    public void Material504_SuccessRoll_ShiftsItemIdWithNoEnchantDecrease()
    {
        var item = WarlordItem();

        var result = SkyUpgradeResolver.Resolve(item, 35, 504, new ScriptedRandomSource(0));

        Assert.True(result.Succeeded);
        Assert.Equal(87129, result.NewItemId);
        Assert.Equal(35, result.NewEnchant);
    }
}
