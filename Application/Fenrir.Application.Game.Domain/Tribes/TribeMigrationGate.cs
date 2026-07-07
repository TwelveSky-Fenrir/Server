namespace Fenrir.Application.Game.Domain.Tribes;

/// <summary>
///     Every character-specific, server-authoritative precondition for CZ_CHANGE_TO_TRIBE4_SEND (op37), pure
///     and I/O-free -- the caller resolves every input (world-state tribe points, alliance graph, session
///     state) before calling <see cref="Evaluate" />. Does NOT include the shared global conversion quota
///     (Server/ts25extra/S08_MyDB.cpp:1193-1215) -- that is I/O by nature and is checked by the Services layer
///     only after this gate already returned <see cref="TribeMigrationOutcome.Success" />, deliberately LAST
///     rather than legacy's own quota-first ordering. See <see cref="TribeMigrationOutcome.QuotaExhausted" />'s
///     own remarks for why: the legacy consumes the shared quota before any character-specific gate runs, so
///     an otherwise-fully-ineligible player can still spend down a quota shared by every other attempt during
///     the same window -- a fairness/availability defect the behavior contract explicitly flags as something
///     NOT to port as-is.
/// </summary>
/// <remarks>
///     Réf. "Fourth-tribe (Fujin) conversion and return" behavior contract, itself citing
///     Server/ts25zone/S04_MyWork02.cpp:7565-7758 (full precondition chain) and
///     Server/ts25zone/S07_MyGame03.cpp:4888-4891 (CheckChangeTribeTime). Preconditions #2 (realm/tribe-
///     routing consistency: the zone's configured server number must match a fixed per-tribe realm --
///     71/72/73/140) and #3 (tribe-4 realm reachability) are not modeled anywhere in this type: Fenrir shards
///     by map, never by a numbered server-instance-per-tribe, so there is no "realm" for a tribe-3 character
///     to be routed to or from -- every character, of every tribe, lives in the same shared world. This is
///     the same translation <c>GameServerOptions.VoteTribeEnabled</c>/<c>HolyStoneBattleEnabled</c> already
///     apply to their own legacy "server number 37/38" gates.
/// </remarks>
public static class TribeMigrationGate
{
    /// <summary>LV_M33, Server/Header/Protocol/DEFINE.h:483.</summary>
    public const short MinLevel = 145;

    /// <summary>Outbound branch's minimum own-tribe world-wide point total. S04_MyWork02.cpp:7652-7656.</summary>
    public const int MinOutboundTribePoints = 100;

    /// <summary>The neutral/"Fujin" faction id -- never a combat tribe.</summary>
    public const byte TribeFour = 3;

    public static TribeMigrationOutcome Evaluate(TribeMigrationEligibilityContext ctx)
    {
        if (!ctx.FeatureEnabled)
            return TribeMigrationOutcome.FeatureDisabled;

        var outbound = ctx.CurrentTribe != TribeFour;

        // Time-window and (the architecturally-N/A) realm-reachability gates apply to the outbound branch
        // only -- the behavior contract is explicit that a Fujin member returning home is not time-gated.
        if (outbound && !IsWithinConversionWindow(ctx.NowLocal))
            return TribeMigrationOutcome.OutsideConversionWindow;

        if (ctx.Level < MinLevel)
            return TribeMigrationOutcome.LevelTooLow;

        var branchOutcome = outbound ? EvaluateOutbound(ctx) : EvaluateReturn(ctx);
        if (branchOutcome != TribeMigrationOutcome.Success)
            return branchOutcome;

        return EvaluateCommon(ctx);
    }

    /// <summary>
    ///     Saturday, 16:00:00 through 18:59:59 inclusive, against the caller-supplied local wall clock --
    ///     mirrors the legacy's own hour-only granularity (no finer minute/second boundary exists).
    /// </summary>
    public static bool IsWithinConversionWindow(DateTime nowLocal)
    {
        return nowLocal.DayOfWeek == DayOfWeek.Saturday && nowLocal.Hour is >= 16 and <= 18;
    }

    private static TribeMigrationOutcome EvaluateOutbound(TribeMigrationEligibilityContext ctx)
    {
        // "Fujin Only" -- tribe 1 can never defect into tribe 3. S04_MyWork02.cpp:7643-7651.
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
        // Defensive, Fenrir-only hardening -- see TribeMigrationOutcome.InvalidPreviousTribe's own remarks.
        // Guards the TribePoints[PreviousTribe] index below from ever going out of bounds.
        if (ctx.PreviousTribe > 2)
            return TribeMigrationOutcome.InvalidPreviousTribe;

        // Blocks the return specifically when the origin tribe is STRICTLY ahead of tribe 3 in points --
        // S04_MyWork02.cpp:7689-7693.
        if (ctx.TribePoints[ctx.PreviousTribe] > ctx.TribePoints[TribeFour])
            return TribeMigrationOutcome.OriginTribeAheadOfTribeThree;

        if (ctx.ReturnAllowance < 1)
            return TribeMigrationOutcome.NoReturnAllowance;

        return TribeMigrationOutcome.Success;
    }

    private static TribeMigrationOutcome EvaluateCommon(TribeMigrationEligibilityContext ctx)
    {
        // Blocks all three tiers of ReturnTribeRole -- master (1), sub-master (2), and elected vote candidate
        // (3) -- matching PlayerRuntimeState.TribeRole's own encoding directly. S04_MyWork02.cpp:7701-7705.
        if (ctx.TribeRole != 0)
            return TribeMigrationOutcome.HoldsTribeRole;

        if (ctx.GuildId is not null || ctx.TeacherCharacterId is not null || ctx.StudentCharacterId is not null)
            return TribeMigrationOutcome.HasGuildOrMentorLink;

        if (ctx.HasAnyFriend)
            return TribeMigrationOutcome.HasRegisteredFriends;

        return TribeMigrationOutcome.Success;
    }

    /// <summary>
    ///     CheckPossibleChangeToTribe4 (Server/Header/function.h:3014-3045): fails (returns false) if tribe 3's
    ///     own point total is already at or ahead of the strongest alliance-adjusted combat-tribe bloc, or if
    ///     the character's own tribe is the strict alliance-adjusted weakest of the three. An alliance-adjusted
    ///     total is a tribe's own points plus its current ally's (if any) -- <paramref name="ctx" />'s
    ///     <see cref="TribeMigrationEligibilityContext.AllyOf" /> resolves that pairing. Ties (no single
    ///     strict weakest) do not block: only a tribe strictly behind BOTH others counts as "the weakest" here,
    ///     mirroring <see cref="IsRawDominantTribe" />'s own strict-uniqueness reading of "the single X tribe."
    /// </summary>
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
                return true; // not strictly behind this rival -- not the unique weakest
        }

        return false; // strictly behind both rivals
    }

    /// <summary>
    ///     ReturnBigTribe (Server/Header/function.h:3124-3144): true only when <paramref name="tribe" />'s raw
    ///     point total is strictly greater than both other combat tribes' -- a tie for first is not
    ///     "dominant." Server/ts25zone/S04_MyWork02.cpp:7665-7672 (call site).
    /// </summary>
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

/// <summary>
///     Every server-authoritative input <see cref="TribeMigrationGate.Evaluate" /> needs, already resolved by
///     the caller -- never anything the client asserts. <see cref="TribePoints" /> is indexed 0-3 (tribe id);
///     <see cref="AllyOf" /> mirrors <c>WorldStateService.GetAllyOf</c> exactly (the other tribe of whichever
///     active alliance pair a given tribe belongs to, or null).
/// </summary>
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
