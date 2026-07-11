using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.Combat;

public class OneShotKillClassifierTests
{
    [Fact]
    public void KillCpType_MatchesLegacyEnumOrdering()
    {
        Assert.Equal((byte)0, (byte)KillCpType.Stun);
        Assert.Equal((byte)1, (byte)KillCpType.NormalHit);
        Assert.Equal((byte)2, (byte)KillCpType.CriticalHit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(115)]
    public void ReflectKill_IsAlwaysCritical_RegardlessOfSkillIndex(int killingSkillIndex)
    {
        Assert.Equal(KillCpType.CriticalHit,
            OneShotKillClassifier.Classify(killingSkillIndex, true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(115)]
    public void NonReflectKill_IsNormal_WhileOneShotSetIsEmpty(int killingSkillIndex)
    {
        Assert.Equal(KillCpType.NormalHit,
            OneShotKillClassifier.Classify(killingSkillIndex, false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(115)]
    [InlineData(999)]
    public void IsOneShotSkill_False_ForEveryIndex_WhileSetIsEmpty(int skillIndex)
    {
        Assert.False(OneShotKillClassifier.IsOneShotSkill(skillIndex));
    }

    [Fact]
    public void Classify_NeverReturnsStun()
    {
        Assert.NotEqual(KillCpType.Stun, OneShotKillClassifier.Classify(42, false));
        Assert.NotEqual(KillCpType.Stun, OneShotKillClassifier.Classify(42, true));
    }
}
