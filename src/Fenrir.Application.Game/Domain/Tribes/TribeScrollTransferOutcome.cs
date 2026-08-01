namespace Fenrir.Application.Game.Domain.Tribes;

public enum TribeScrollTransferOutcome
{
    Success,

    InvalidDestinationTribe,

    AlreadyTargetTribe,

    LevelTooLow,

    HomeZoneOffline,

    WrongLocation,

    HoldsTribeRole,

    IsMentor,

    IsMentee,

    InParty,

    HasGuild,

    CapeEquipped,

    HasRegisteredFriends
}
