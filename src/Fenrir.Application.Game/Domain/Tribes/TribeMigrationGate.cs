namespace Fenrir.Application.Game.Domain.Tribes;

public static class TribeMigrationGate
{
    public const short MinLevel = 145;

    public const int MinOutboundTribePoints = 100;

    public const byte TribeFour = 3;

    public static TribeMigrationOutcome Evaluate(TribeMigrationEligibilityContext ctx)
    {
        if (!ctx.FeatureEnabled)
            return TribeMigrationOutcome.FeatureDisabled;

        var outbound = ctx.CurrentTribe != TribeFour;

        if (outbound && !IsWithinConversionWindow(ctx.NowLocal))
            return TribeMigrationOutcome.OutsideConversionWindow;

        if (ctx.Level < MinLevel)
            return TribeMigrationOutcome.LevelTooLow;

        var branchOutcome = outbound ? EvaluateOutbound(ctx) : EvaluateReturn(ctx);
        if (branchOutcome != TribeMigrationOutcome.Success)
            return branchOutcome;

        return EvaluateCommon(ctx);
    }

    public static bool IsWithinConversionWindow(DateTime nowLocal)
    {
        return nowLocal.DayOfWeek == DayOfWeek.Saturday && nowLocal.Hour is >= 16 and <= 18;
    }

    private static TribeMigrationOutcome EvaluateOutbound(TribeMigrationEligibilityContext ctx)
    {
        if (ctx.CurrentTribe == 1)
            return TribeMigrationOutcome.Tribe1CannotJoinTribeFour;

        if (ctx.TribePoints[ctx.CurrentTribe] < MinOutboundTribePoints)
            return TribeMigrationOutcome.TribePointsBelowThreshold;

        if (!PassesAllianceAdjustedWorldState(ctx))
            return TribeMigrationOutcome.NotEligibleByWorldState;

        if (!IsRawDominantTribe(ctx.CurrentTribe, ctx.TribePoints))
            return TribeMigrationOutcome.NotDominantTribe;

        return TribeMigrationOutcome.Success;
    }

    private static TribeMigrationOutcome EvaluateReturn(TribeMigrationEligibilityContext ctx)
    {
        if (ctx.PreviousTribe > 2)
            return TribeMigrationOutcome.InvalidPreviousTribe;

        if (ctx.TribePoints[ctx.PreviousTribe] > ctx.TribePoints[TribeFour])
            return TribeMigrationOutcome.OriginTribeAheadOfTribeThree;

        if (ctx.ReturnAllowance < 1)
            return TribeMigrationOutcome.NoReturnAllowance;

        return TribeMigrationOutcome.Success;
    }

    private static TribeMigrationOutcome EvaluateCommon(TribeMigrationEligibilityContext ctx)
    {
        if (ctx.TribeRole != 0)
            return TribeMigrationOutcome.HoldsTribeRole;

        if (ctx.GuildId is not null || ctx.TeacherCharacterId is not null || ctx.StudentCharacterId is not null)
            return TribeMigrationOutcome.HasGuildOrMentorLink;

        if (ctx.HasAnyFriend)
            return TribeMigrationOutcome.HasRegisteredFriends;

        return TribeMigrationOutcome.Success;
    }

    private static bool PassesAllianceAdjustedWorldState(TribeMigrationEligibilityContext ctx)
    {
        var adjusted = new int[3];
        for (byte tribe = 0; tribe < 3; tribe++)
        {
            adjusted[tribe] = ctx.TribePoints[tribe];
            if (ctx.AllyOf(tribe) is { } ally && ally < 3)
                adjusted[tribe] += ctx.TribePoints[ally];
        }

        var strongestBloc = Math.Max(adjusted[0], Math.Max(adjusted[1], adjusted[2]));
        if (ctx.TribePoints[TribeFour] >= strongestBloc)
            return false;

        var mine = adjusted[ctx.CurrentTribe];
        for (byte tribe = 0; tribe < 3; tribe++)
        {
            if (tribe == ctx.CurrentTribe)
                continue;
            if (mine >= adjusted[tribe])
                return true;
        }

        return false;
    }

    private static bool IsRawDominantTribe(byte tribe, IReadOnlyList<int> tribePoints)
    {
        var mine = tribePoints[tribe];
        for (byte other = 0; other < 3; other++)
        {
            if (other == tribe)
                continue;
            if (tribePoints[other] >= mine)
                return false;
        }

        return true;
    }
}

public readonly record struct TribeMigrationEligibilityContext(
    bool FeatureEnabled,
    DateTime NowLocal,
    byte CurrentTribe,
    byte PreviousTribe,
    short Level,
    byte TribeRole,
    int? GuildId,
    int? TeacherCharacterId,
    int? StudentCharacterId,
    bool HasAnyFriend,
    int ReturnAllowance,
    IReadOnlyList<int> TribePoints,
    Func<byte, byte?> AllyOf);
