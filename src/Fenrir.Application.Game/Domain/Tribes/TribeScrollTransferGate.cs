namespace Fenrir.Application.Game.Domain.Tribes;

public static class TribeScrollTransferGate
{
    public const short MinLevel = 145;

    public static TribeScrollTransferOutcome Evaluate(TribeScrollTransferEligibilityContext ctx)
    {
        if (!TribeConversionResolver.IsPlayableTribe(ctx.ToTribe))
            return TribeScrollTransferOutcome.InvalidDestinationTribe;

        if (ctx.ToTribe == ctx.PreviousTribe)
            return TribeScrollTransferOutcome.AlreadyTargetTribe;

        if (ctx.Level < MinLevel)
            return TribeScrollTransferOutcome.LevelTooLow;

        if (!ctx.DestinationTribeOpen)
            return TribeScrollTransferOutcome.DestinationTribeClosed;

        if (!ctx.HomeZoneOnline)
            return TribeScrollTransferOutcome.HomeZoneOffline;

        if (!IsValidTown(ctx.CurrentTribe, ctx.MapId))
            return TribeScrollTransferOutcome.WrongLocation;

        if (ctx.TribeRole != 0)
            return TribeScrollTransferOutcome.HoldsTribeRole;

        if (ctx.StudentCharacterId is not null)
            return TribeScrollTransferOutcome.IsMentor;

        if (ctx.TeacherCharacterId is not null)
            return TribeScrollTransferOutcome.IsMentee;

        if (ctx.InParty)
            return TribeScrollTransferOutcome.InParty;

        if (ctx.GuildId is not null)
            return TribeScrollTransferOutcome.HasGuild;

        if (ctx.CapeEquipped)
            return TribeScrollTransferOutcome.CapeEquipped;

        if (ctx.HasAnyFriend)
            return TribeScrollTransferOutcome.HasRegisteredFriends;

        return TribeScrollTransferOutcome.Success;
    }

    public static bool IsValidTown(byte tribe, short mapId)
    {
        return tribe switch
        {
            0 => mapId == 1,
            1 => mapId == 6,
            2 => mapId == 11,
            3 => mapId == 140,
            _ => false
        };
    }
}

public readonly record struct TribeScrollTransferEligibilityContext(
    byte ToTribe,
    byte CurrentTribe,
    byte PreviousTribe,
    short Level,
    byte TribeRole,
    int? GuildId,
    int? TeacherCharacterId,
    int? StudentCharacterId,
    bool HasAnyFriend,
    bool InParty,
    bool CapeEquipped,
    short MapId,
    bool DestinationTribeOpen,
    bool HomeZoneOnline);
