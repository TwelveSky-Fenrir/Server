using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Forge;

public class RankChangeResolverWarlordTests
{
    private const byte CapeSort = 8;
    private const byte NonCapeSort = 9;

    private static ItemDefinition Def(byte type, short level, byte martialLevel, byte sort)
    {
        var row = WorldDataTestRows.Item(level * 1000 + martialLevel * 10 + sort) with
        {
            Type = type, Sort = sort, Level = level, MartialLevel = martialLevel, EquipInfo1 = 1,
            CheckHighItem = 2, CheckLowItem = 2, CheckAvatarTrade = 0, CheckMonsterDrop = 0
        };
        return new ItemDefinition(row, []);
    }

    private static ItemStack Stack()
    {
        return new ItemStack(1, 1, 4, 1, 0, 0, 0, 0, 0, 0, 999);
    }

    private static ItemRowDto Material1024()
    {
        return WorldDataTestRows.Item(1024);
    }

    [Fact]
    public void TopTierSuccess_NonCape_RandModFourNonZero_FlagsWarlordEligible()
    {
        var target = Def(RankChangeResolver.RareItemType, 145, 11, NonCapeSort);
        var candidate = Def(RankChangeResolver.RareItemType, 145, 12, NonCapeSort);

        var result = RankChangeResolver.ResolveUpgrade(target, Stack(), Material1024(), 0, 0,
            [target, candidate], new ScriptedRandomSource(0, 5, 3));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Success, result.Outcome);
        Assert.True(result.WarlordRerollEligible);
    }

    [Fact]
    public void TopTierSuccess_NonCape_RandModFourZero_NotEligible()
    {
        var target = Def(RankChangeResolver.RareItemType, 145, 11, NonCapeSort);
        var candidate = Def(RankChangeResolver.RareItemType, 145, 12, NonCapeSort);

        var result = RankChangeResolver.ResolveUpgrade(target, Stack(), Material1024(), 0, 0,
            [target, candidate], new ScriptedRandomSource(0, 5, 4));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Success, result.Outcome);
        Assert.False(result.WarlordRerollEligible);
    }

    [Fact]
    public void TopTierSuccess_Cape_NeverEligible_NoWarlordDrawTaken()
    {
        var target = Def(RankChangeResolver.RareItemType, 145, 11, CapeSort);
        var candidate = Def(RankChangeResolver.RareItemType, 145, 12, CapeSort);

        var result = RankChangeResolver.ResolveUpgrade(target, Stack(), Material1024(), 0, 0,
            [target, candidate], new ScriptedRandomSource(0, 5, 3));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Success, result.Outcome);
        Assert.False(result.WarlordRerollEligible);
    }

    [Fact]
    public void BelowTopTierSuccess_NeverEligible()
    {
        var target = Def(RankChangeResolver.RareItemType, 45, 0, NonCapeSort);
        var candidate = Def(RankChangeResolver.RareItemType, 55, 0, NonCapeSort);

        var result = RankChangeResolver.ResolveUpgrade(target, Stack(), Material1024(), 0, 0,
            [target, candidate], new ScriptedRandomSource(0, 5, 3));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Success, result.Outcome);
        Assert.False(result.WarlordRerollEligible);
    }

    [Fact]
    public void Downgrade_NeverEligible()
    {
        var target = Def(RankChangeResolver.RareItemType, 55, 0, NonCapeSort);
        var candidate = Def(RankChangeResolver.RareItemType, 45, 0, NonCapeSort);

        var result = RankChangeResolver.ResolveDowngrade(target, Stack(), Material1024(), 0, 0,
            [target, candidate], new ScriptedRandomSource(0, 10));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Success, result.Outcome);
        Assert.False(result.WarlordRerollEligible);
    }
}
