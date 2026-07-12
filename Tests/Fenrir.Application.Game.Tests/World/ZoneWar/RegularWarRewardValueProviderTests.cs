using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class RegularWarRewardValueProviderTests
{
    private static readonly RegularWarRewardValueProvider Provider = new();

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)50)]
    [InlineData((short)99)]
    [InlineData((short)112)]
    [InlineData((short)113)]
    [InlineData((short)144)]
    public void GetExperienceReward_BelowLevelCap_CeilsToOne(short level)
    {
        Assert.Equal(1, Provider.GetExperienceReward(level));
    }

    [Theory]
    [InlineData((short)145)]
    [InlineData((short)157)]
    [InlineData((short)200)]
    public void GetExperienceReward_AtOrAboveLevelCap_UsesTheSentinel(short level)
    {
        Assert.Equal(20_000_000, Provider.GetExperienceReward(level));
    }

    [Theory]
    [InlineData((short)0, (short)100)]
    [InlineData((short)5, (short)100)]
    [InlineData((short)12, (short)145)]
    public void GetMoneyReward_IsNotYetModeled(short evolutionTier, short level)
    {
        Assert.Equal(0, Provider.GetMoneyReward(evolutionTier, level));
    }
}
