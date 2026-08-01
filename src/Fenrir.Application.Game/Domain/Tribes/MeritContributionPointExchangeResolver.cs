namespace Fenrir.Application.Game.Domain.Tribes;

public static class MeritContributionPointExchangeResolver
{
    public const int MinUnits = 1;

    public const int MaxUnits = 100;

    public const int TeacherPointCostPerUnit = 100_000;

    public const short MinimumLevel = 50;

    public static MeritContributionPointExchangeOutcome Evaluate(int requestedUnits, short level, int teacherPoint)
    {
        if (requestedUnits is < MinUnits or > MaxUnits)
            return MeritContributionPointExchangeOutcome.InvalidUnitCount;

        if (level < MinimumLevel)
            return MeritContributionPointExchangeOutcome.LevelTooLow;

        if (teacherPoint < requestedUnits * TeacherPointCostPerUnit)
            return MeritContributionPointExchangeOutcome.InsufficientTeacherPoints;

        return MeritContributionPointExchangeOutcome.Success;
    }
}

public enum MeritContributionPointExchangeOutcome
{
    Success,

    InvalidUnitCount,

    LevelTooLow,

    InsufficientTeacherPoints
}
