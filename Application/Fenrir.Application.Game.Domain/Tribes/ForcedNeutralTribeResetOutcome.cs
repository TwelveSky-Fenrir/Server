namespace Fenrir.Application.Game.Domain.Tribes;

public enum ForcedNeutralTribeResetOutcome
{
    Success,

        LevelTooLow,

        AlreadyNeutral,

        HoldsTribeRole,

        HasGuildOrMentorLink,

        HasRegisteredFriends,

        NeutralHomeZoneOffline
}
