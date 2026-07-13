namespace Fenrir.Application.Game.Domain.Tribes;

public enum TribeMigrationOutcome
{
    Success,


    FeatureDisabled,

    OutsideConversionWindow,

    Tribe1CannotJoinTribeFour,


    LevelTooLow,

    TribePointsBelowThreshold,

    NotEligibleByWorldState,

    NotDominantTribe,

    OriginTribeAheadOfTribeThree,

    NoReturnAllowance,

    InvalidPreviousTribe,

    HoldsTribeRole,

    HasGuildOrMentorLink,

    HasRegisteredFriends,

    QuotaExhausted
}

public static class TribeMigrationOutcomeExtensions
{
    public static bool RepliesWithFailure(this TribeMigrationOutcome outcome)
    {
        return outcome is TribeMigrationOutcome.FeatureDisabled or TribeMigrationOutcome.OutsideConversionWindow
            or TribeMigrationOutcome.Tribe1CannotJoinTribeFour;
    }
}
