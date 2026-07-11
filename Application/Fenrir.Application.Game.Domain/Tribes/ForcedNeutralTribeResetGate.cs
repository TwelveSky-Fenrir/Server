namespace Fenrir.Application.Game.Domain.Tribes;

public static class ForcedNeutralTribeResetGate
{

        public const short MinLevel = 113;

        public const byte NeutralTribe = TribeMigrationGate.TribeFour;

    public static ForcedNeutralTribeResetOutcome Evaluate(ForcedNeutralTribeResetEligibilityContext ctx)
    {
        if (ctx.Level < MinLevel)
            return ForcedNeutralTribeResetOutcome.LevelTooLow;

        if (ctx.CurrentTribe == NeutralTribe)
            return ForcedNeutralTribeResetOutcome.AlreadyNeutral;

        if (ctx.TribeRole != 0)
            return ForcedNeutralTribeResetOutcome.HoldsTribeRole;

        if (ctx.GuildId is not null || ctx.TeacherCharacterId is not null || ctx.StudentCharacterId is not null)
            return ForcedNeutralTribeResetOutcome.HasGuildOrMentorLink;

        if (ctx.HasAnyFriend)
            return ForcedNeutralTribeResetOutcome.HasRegisteredFriends;

        if (!ctx.NeutralHomeZoneOnline)
            return ForcedNeutralTribeResetOutcome.NeutralHomeZoneOffline;

        return ForcedNeutralTribeResetOutcome.Success;
    }
}

public readonly record struct ForcedNeutralTribeResetEligibilityContext(
    short Level,
    byte CurrentTribe,
    byte TribeRole,
    int? GuildId,
    int? TeacherCharacterId,
    int? StudentCharacterId,
    bool HasAnyFriend,
    bool NeutralHomeZoneOnline);
