using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class HighLevelExperienceResolverTests
{
    private const long Floor145 = 1_000_000_000;

    private static HighLevelExperienceInput At(short level, long mainExp, short level2, int exp2, int gain,
        bool antiCheat = false)
    {
        return new HighLevelExperienceInput(level, mainExp, Floor145, level2, exp2, gain, antiCheat);
    }


    [Fact]
    public void Ceiling_EqualsLevel12Threshold()
    {
        Assert.Equal(HighLevelExperienceResolver.RebirthTierExperienceCeiling,
            RebirthProgression.HighLevelExpTable[RebirthProgression.MaxHighLevel - 1]);
    }

    [Fact]
    public void Threshold_Level0_IsZero()
    {
        Assert.Equal(0, HighLevelExperienceResolver.RebirthTierThreshold(0));
    }

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)6)]
    [InlineData((short)12)]
    public void Threshold_ForLevel_MatchesHighLevelExpTable(short level2)
    {
        Assert.Equal(RebirthProgression.HighLevelExpTable[level2 - 1],
            HighLevelExperienceResolver.RebirthTierThreshold(level2));
    }

    [Theory]
    [InlineData((short)144, false)]
    [InlineData((short)145, true)]
    [InlineData((short)146, true)]
    public void AppliesAt_OnlyAtOrAboveGeneralCap(short level, bool expected)
    {
        Assert.Equal(expected, HighLevelExperienceResolver.AppliesAt(level));
    }


    [Fact]
    public void Resolve_BelowGeneralCap_IsNone()
    {
        var outcome = HighLevelExperienceResolver.Resolve(At(144, 500, level2: 0, exp2: 0, gain: 1000));

        Assert.Equal(HighLevelExperienceOutcomeKind.None, outcome.Kind);
    }


    [Fact]
    public void Resolve_StageA_FillsPoolAndGrantsStatPointsPerPercent()
    {
        var outcome = HighLevelExperienceResolver.Resolve(
            At(145, mainExp: 1_000_000_000, level2: 0, exp2: 0, gain: 900_000_000));

        Assert.Equal(HighLevelExperienceOutcomeKind.MainPoolFill, outcome.Kind);
        Assert.Equal(1_900_000_000L, outcome.NewMainExperience);
        Assert.Equal(90, outcome.StatPointsGranted);
    }

    [Fact]
    public void Resolve_StageA_ClampsPoolAtCeiling()
    {
        var outcome = HighLevelExperienceResolver.Resolve(
            At(145, mainExp: 1_900_000_000, level2: 0, exp2: 0, gain: 500_000_000));

        Assert.Equal(HighLevelExperienceOutcomeKind.MainPoolFill, outcome.Kind);
        Assert.Equal(HighLevelExperienceResolver.MaxMainExperience, outcome.NewMainExperience);
        Assert.Equal(10, outcome.StatPointsGranted);
    }

    [Fact]
    public void Resolve_StageA_AntiCheatFlag_IsIgnored()
    {
        var outcome = HighLevelExperienceResolver.Resolve(
            At(145, mainExp: 1_000_000_000, level2: 0, exp2: 0, gain: 100_000_000, antiCheat: true));

        Assert.Equal(HighLevelExperienceOutcomeKind.MainPoolFill, outcome.Kind);
        Assert.Equal(1_100_000_000L, outcome.NewMainExperience);
    }


    [Fact]
    public void Resolve_StageB_Level0_LevelsUpImmediatelyWithZone101Bonus()
    {
        var outcome = HighLevelExperienceResolver.Resolve(
            At(145, mainExp: HighLevelExperienceResolver.MaxMainExperience, level2: 0, exp2: 0, gain: 5000));

        Assert.Equal(HighLevelExperienceOutcomeKind.RebirthTierLevelUp, outcome.Kind);
        Assert.Equal(1, outcome.NewLevel2);
        Assert.Equal(HighLevelExperienceResolver.RebirthTierLevelUpSkillPoints, outcome.SkillPointsGranted);
        Assert.Equal(HighLevelExperienceResolver.Zone101TimeGrantOnFirstRebirthTier, outcome.Zone101TimeBonus);
        Assert.Equal(0, outcome.NewExp2);
    }

    [Fact]
    public void Resolve_StageB_LevelUpFromMidTier_NoZone101Bonus()
    {
        var threshold1 = RebirthProgression.HighLevelExpTable[0];
        var outcome = HighLevelExperienceResolver.Resolve(
            At(145, mainExp: HighLevelExperienceResolver.MaxMainExperience, level2: 1, exp2: threshold1, gain: 50));

        Assert.Equal(HighLevelExperienceOutcomeKind.RebirthTierLevelUp, outcome.Kind);
        Assert.Equal(2, outcome.NewLevel2);
        Assert.Equal(100, outcome.SkillPointsGranted);
        Assert.Equal(0, outcome.Zone101TimeBonus);
    }


    [Fact]
    public void Resolve_StageB_Accrual_GrantsStatPointsAndGrowsPool()
    {
        var threshold1 = RebirthProgression.HighLevelExpTable[0];
        var gain = 100_000_000;
        var outcome = HighLevelExperienceResolver.Resolve(
            At(145, mainExp: HighLevelExperienceResolver.MaxMainExperience, level2: 1, exp2: 0, gain: gain));

        Assert.Equal(HighLevelExperienceOutcomeKind.RebirthTierAccrual, outcome.Kind);
        Assert.Equal(gain, outcome.NewExp2);
        Assert.Equal((int)((long)gain * 100 / threshold1), outcome.StatPointsGranted);
    }

    [Fact]
    public void Resolve_StageB_Accrual_ClampsGainToCurrentLevelThreshold()
    {
        var threshold1 = RebirthProgression.HighLevelExpTable[0];
        var outcome = HighLevelExperienceResolver.Resolve(
            At(145, mainExp: HighLevelExperienceResolver.MaxMainExperience, level2: 1, exp2: 0,
                gain: threshold1 + 500_000));

        Assert.Equal(HighLevelExperienceOutcomeKind.RebirthTierAccrual, outcome.Kind);
        Assert.Equal(threshold1, outcome.NewExp2);
        Assert.Equal(100, outcome.StatPointsGranted);
    }

    [Fact]
    public void Resolve_StageB_LevelUpFiresTickAfterThresholdReached()
    {
        var threshold1 = RebirthProgression.HighLevelExpTable[0];
        var outcome = HighLevelExperienceResolver.Resolve(
            At(145, mainExp: HighLevelExperienceResolver.MaxMainExperience, level2: 1, exp2: threshold1,
                gain: 1_000_000));

        Assert.Equal(HighLevelExperienceOutcomeKind.RebirthTierLevelUp, outcome.Kind);
        Assert.Equal(2, outcome.NewLevel2);
    }


    [Fact]
    public void Resolve_StageB_Level12_AtCeiling_IsNone()
    {
        var outcome = HighLevelExperienceResolver.Resolve(
            At(145, mainExp: HighLevelExperienceResolver.MaxMainExperience, level2: 12,
                exp2: HighLevelExperienceResolver.RebirthTierExperienceCeiling, gain: 1_000_000));

        Assert.Equal(HighLevelExperienceOutcomeKind.None, outcome.Kind);
    }

    [Fact]
    public void Resolve_StageB_Level12_BelowCeiling_Accrues()
    {
        var ceiling = HighLevelExperienceResolver.RebirthTierExperienceCeiling;
        var outcome = HighLevelExperienceResolver.Resolve(
            At(145, mainExp: HighLevelExperienceResolver.MaxMainExperience, level2: 12, exp2: 1_000_000_000,
                gain: 100_000_000));

        Assert.Equal(HighLevelExperienceOutcomeKind.RebirthTierAccrual, outcome.Kind);
        Assert.Equal(1_100_000_000, outcome.NewExp2);
        var present = (int)(1_000_000_000L * 100 / ceiling);
        var next = (int)(1_100_000_000L * 100 / ceiling);
        Assert.Equal(next - present, outcome.StatPointsGranted);
    }

    [Fact]
    public void Resolve_StageB_GlobalCeilingClamp_LimitsAccrualToCeiling()
    {
        var ceiling = HighLevelExperienceResolver.RebirthTierExperienceCeiling;
        var outcome = HighLevelExperienceResolver.Resolve(
            At(145, mainExp: HighLevelExperienceResolver.MaxMainExperience, level2: 12, exp2: ceiling - 1000,
                gain: 500_000_000));

        Assert.Equal(HighLevelExperienceOutcomeKind.RebirthTierAccrual, outcome.Kind);
        Assert.Equal(ceiling, outcome.NewExp2);
    }


    [Fact]
    public void Resolve_StageB_AntiCheatFlag_IsSilentNoOp()
    {
        var outcome = HighLevelExperienceResolver.Resolve(
            At(145, mainExp: HighLevelExperienceResolver.MaxMainExperience, level2: 3, exp2: 100,
                gain: 5_000_000, antiCheat: true));

        Assert.Equal(HighLevelExperienceOutcomeKind.None, outcome.Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Resolve_StageB_BelowOneGain_IsSilentNoOp(int gain)
    {
        var outcome = HighLevelExperienceResolver.Resolve(
            At(145, mainExp: HighLevelExperienceResolver.MaxMainExperience, level2: 3, exp2: 100, gain: gain));

        Assert.Equal(HighLevelExperienceOutcomeKind.None, outcome.Kind);
    }
}
