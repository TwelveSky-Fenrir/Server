using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.Combat;

/// <summary>
///     Covers <see cref="OneShotKillClassifier" /> and the <see cref="KillCpType" /> enum -- the B14 one-shot /
///     critical PvP kill-type classification (<c>AtkGet1Hit</c>, S07_MyGame02.cpp:811-879,899,1300-1334,
///     1379-1381; <c>KILL_CP_TYPE</c>, H07_MyGame.h:51-55).
/// </summary>
public class OneShotKillClassifierTests
{
    [Fact]
    public void KillCpType_MatchesLegacyEnumOrdering()
    {
        // KILL_CP_TYPE: STUN = 0, NORMAL = 1, CRIT = 2 (H07_MyGame.h:51-55).
        Assert.Equal((byte)0, (byte)KillCpType.Stun);
        Assert.Equal((byte)1, (byte)KillCpType.NormalHit);
        Assert.Equal((byte)2, (byte)KillCpType.CriticalHit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(115)] // an index from the DEAD #ifndef LNW33 block -- must NOT be treated as one-shot
    public void ReflectKill_IsAlwaysCritical_RegardlessOfSkillIndex(int killingSkillIndex)
    {
        Assert.Equal(KillCpType.CriticalHit,
            OneShotKillClassifier.Classify(killingSkillIndex, isReflectKill: true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(115)] // dead-block index -- not part of the live one-shot set, so a plain normal kill
    public void NonReflectKill_IsNormal_WhileOneShotSetIsEmpty(int killingSkillIndex)
    {
        // The live AtkGet1Hit membership set is an unrecovered gap (empty), so every ordinary lethal blow
        // classifies as NormalHit until a follow-up finding enumerates the live members.
        Assert.Equal(KillCpType.NormalHit,
            OneShotKillClassifier.Classify(killingSkillIndex, isReflectKill: false));
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
        // Stun is produced only by the separate non-death team-stun credit path, never by an HP-death blow.
        Assert.NotEqual(KillCpType.Stun, OneShotKillClassifier.Classify(42, isReflectKill: false));
        Assert.NotEqual(KillCpType.Stun, OneShotKillClassifier.Classify(42, isReflectKill: true));
    }
}
