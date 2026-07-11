using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Forge;

/// <summary>
///     MG5ORIGIN premium price override in GetExchangeMoney (Server/Header/function.h:1489-1497,1584-1592): a
///     set item at the absolute top tier (level 145, martial 12) costs a flat 250,000,000 (Rare) /
///     500,000,000 (Elite) instead of the normal 7,000,000 / 14,000,000 table value. Exercised through
///     <see cref="RerollResolver.Resolve" /> since the pricing helper itself is private; the reported cost is
///     computed before the replacement draw, so an empty catalog (NoCandidate) still surfaces it.
/// </summary>
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
        // Level 145 martial 11 (one below the top): the override does not apply, so the normal table value
        // (4,000,000 + 400,000 * (13 + 11) = 13,600,000) stands even though it is a set item.
        Assert.Equal(13_600_000, ResolveCost(Target(RerollResolver.EliteItemType, 145, 11, 2)));
    }
}
