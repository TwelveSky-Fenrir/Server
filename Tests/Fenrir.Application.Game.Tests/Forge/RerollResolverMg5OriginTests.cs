using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Forge;

public class RerollResolverMg5OriginTests
{
    private static ItemRowDto Target(byte type, short level, byte martialLevel, byte checkSetItem)
    {
        return WorldDataTestRows.Item(1) with
        {
            Type = type, Sort = 9, Level = level, MartialLevel = martialLevel, CheckSetItem = checkSetItem,
            CheckExchange = 2
        };
    }

    private static int ResolveCost(ItemRowDto target)
    {
        return RerollResolver.Resolve(target, 0, [], new ScriptedRandomSource(0)).Cost;
    }

    [Fact]
    public void EliteSetItem_AtTopTier_UsesFiveHundredMillionOverride()
    {
        Assert.Equal(500_000_000, ResolveCost(Target(RerollResolver.EliteItemType, 145, 12, 2)));
    }

    [Fact]
    public void RareSetItem_AtTopTier_UsesTwoHundredFiftyMillionOverride()
    {
        Assert.Equal(250_000_000, ResolveCost(Target(RerollResolver.RareItemType, 145, 12, 2)));
    }

    [Fact]
    public void EliteNonSetItem_AtTopTier_UsesNormalFourteenMillion()
    {
        Assert.Equal(14_000_000, ResolveCost(Target(RerollResolver.EliteItemType, 145, 12, 0)));
    }

    [Fact]
    public void RareNonSetItem_AtTopTier_UsesNormalSevenMillion()
    {
        Assert.Equal(7_000_000, ResolveCost(Target(RerollResolver.RareItemType, 145, 12, 0)));
    }

    [Fact]
    public void SetItem_BelowTopMartialTier_IsNotOverridden()
    {
        Assert.Equal(13_600_000, ResolveCost(Target(RerollResolver.EliteItemType, 145, 11, 2)));
    }
}
