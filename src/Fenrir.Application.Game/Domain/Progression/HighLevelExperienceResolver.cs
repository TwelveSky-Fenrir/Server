using Fenrir.Domain.Game.Stats;

namespace Fenrir.Application.Game.Domain.Progression;

public enum HighLevelExperienceOutcomeKind : byte
{
    None = 0,

    MainPoolFill = 1,

    RebirthTierLevelUp = 2,

    RebirthTierAccrual = 3
}

public readonly record struct HighLevelExperienceInput(
    short Level,
    long MainExperience,
    long MainExperienceFloor,
    short Level2,
    int Exp2,
    int AwardedExperience,
    bool AntiCheatExperienceFlagged = false);

public readonly record struct HighLevelExperienceOutcome
{
    public required HighLevelExperienceOutcomeKind Kind { get; init; }

    public long NewMainExperience { get; init; }

    public short NewLevel2 { get; init; }

    public int NewExp2 { get; init; }

    public int SkillPointsGranted { get; init; }

    public int StatPointsGranted { get; init; }

    public int Zone101TimeBonus { get; init; }

    public static HighLevelExperienceOutcome None => new() { Kind = HighLevelExperienceOutcomeKind.None };

    public static HighLevelExperienceOutcome MainPoolFill(long newMainExperience, int statPointsGranted)
    {
        return new HighLevelExperienceOutcome
        {
            Kind = HighLevelExperienceOutcomeKind.MainPoolFill,
            NewMainExperience = newMainExperience,
            StatPointsGranted = statPointsGranted
        };
    }

    public static HighLevelExperienceOutcome LevelUp(short newLevel2, int skillPointsGranted, int zone101TimeBonus)
    {
        return new HighLevelExperienceOutcome
        {
            Kind = HighLevelExperienceOutcomeKind.RebirthTierLevelUp,
            NewLevel2 = newLevel2,
            NewExp2 = 0,
            SkillPointsGranted = skillPointsGranted,
            Zone101TimeBonus = zone101TimeBonus
        };
    }

    public static HighLevelExperienceOutcome Accrual(int newExp2, int statPointsGranted)
    {
        return new HighLevelExperienceOutcome
        {
            Kind = HighLevelExperienceOutcomeKind.RebirthTierAccrual,
            NewExp2 = newExp2,
            StatPointsGranted = statPointsGranted
        };
    }
}

public static class HighLevelExperienceResolver
{
    public const long MaxMainExperience = 2_000_000_000L;

    public const int RebirthTierExperienceCeiling = 1_481_117_817;

    public const int RebirthTierLevelUpSkillPoints = 100;

    public const int Zone101TimeGrantOnFirstRebirthTier = 10_000;

    public static bool AppliesAt(short level)
    {
        return level >= LevelProgressionCalculator.MaxLevel;
    }

    public static HighLevelExperienceOutcome Resolve(in HighLevelExperienceInput input)
    {
        if (input.Level < LevelProgressionCalculator.MaxLevel)
            return HighLevelExperienceOutcome.None;

        if (input.MainExperience < MaxMainExperience)
            return ResolveMainPoolFill(input.MainExperience, input.MainExperienceFloor, input.AwardedExperience);

        return HighLevelExperienceOutcome.None;
    }

    public static int RebirthTierThreshold(short level2)
    {
        if (level2 <= 0)
            return 0;
        if (level2 > RebirthProgression.MaxHighLevel)
            return RebirthTierExperienceCeiling;
        return RebirthProgression.HighLevelExpTable[level2 - 1];
    }

    private static HighLevelExperienceOutcome ResolveMainPoolFill(long mainExperience, long mainExperienceFloor,
        int gain)
    {
        if (gain < 1)
            return HighLevelExperienceOutcome.None;

        var newMain = mainExperience + gain;
        if (newMain > MaxMainExperience)
            newMain = MaxMainExperience;

        var statPoints = 0;
        var band = MaxMainExperience - mainExperienceFloor;
        if (band > 0)
        {
            var range = (float)band;
            var presentPercent = (int)((mainExperience - mainExperienceFloor) * 100.0f / range);
            var nextPercent = (int)((newMain - mainExperienceFloor) * 100.0f / range);
            statPoints = nextPercent - presentPercent;
            if (statPoints < 0)
                statPoints = 0;
        }

        return HighLevelExperienceOutcome.MainPoolFill(newMain, statPoints);
    }

    private static int PercentOfThreshold(int exp2, int threshold)
    {
        if (threshold <= 0)
            return 0;
        return (int)(exp2 * 100.0f / threshold);
    }
}
