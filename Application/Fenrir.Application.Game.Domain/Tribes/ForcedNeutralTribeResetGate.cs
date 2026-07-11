namespace Fenrir.Application.Game.Domain.Tribes;

/// <summary>
///     Every character-specific, server-authoritative precondition for using catalog item 8100 (the
///     forced-neutral-tribe-reset consumable), pure and I/O-free -- the caller resolves every input
///     (character facts already loaded on <see cref="World.PlayerRuntimeState" />, plus the one
///     cross-shard reachability fact) before calling <see cref="Evaluate" />.
///     <para>
///         Deliberately NOT the same mechanism as <see cref="TribeMigrationGate" /> (CZ_CHANGE_TO_TRIBE4_SEND,
///         op37): that is a distinct, client-initiated opcode with a much larger precondition set (level 145,
///         tribe-point standings, alliance-adjusted world state, a raw-dominant-tribe check, a Saturday
///         16:00-18:59 time window, a shared daily quota, a return-allowance charge). Item 8100 is a plain
///         op23 consumable use with a lower level floor (113) and none of those world-state/quota/time-window
///         gates -- confirmed by reading both branches side by side; see this type's own member remarks for
///         the item-8100-specific citations. The two mechanisms happen to share three gates in almost
///         identical shape (tribe-role, guild/mentor link, friend list) because both ultimately consult the
///         same legacy <c>ReturnTribeRole</c>/<c>aFriend</c>/<c>aTeacher</c>/<c>aStudent</c> fields -- this is
///         convergent reuse of the same character facts, not evidence the two mechanisms are one gate wearing
///         two opcodes.
///     </para>
/// </summary>
/// <remarks>
///     Réf. "Forced-neutral tribe reset (item 8100 use)" behavior contract (legacy-behavior-translator),
///     itself citing Server/ts25zone/S04_MyWork03.cpp:7225-7281 (the full item-8100 case body) throughout.
/// </remarks>
public static class ForcedNeutralTribeResetGate
{
    /// <summary>LV_M1, Server/Header/Protocol/DEFINE.h:451.</summary>
    public const short MinLevel = 113;

    /// <summary>
    ///     The neutral/"Fujin" faction id -- the same value <see cref="TribeMigrationGate.TribeFour" /> uses
    ///     for the unrelated op37 mechanism, reused here rather than redefined.
    /// </summary>
    public const byte NeutralTribe = TribeMigrationGate.TribeFour;

    public static ForcedNeutralTribeResetOutcome Evaluate(ForcedNeutralTribeResetEligibilityContext ctx)
    {
        // S04_MyWork03.cpp:7226 -- only the primary level field is read; a secondary level field some other
        // systems track, and any rebirth-count field, are both left unchecked by the legacy branch itself.
        if (ctx.Level < MinLevel)
            return ForcedNeutralTribeResetOutcome.LevelTooLow;

        // S04_MyWork03.cpp:7231-7235.
        if (ctx.CurrentTribe == NeutralTribe)
            return ForcedNeutralTribeResetOutcome.AlreadyNeutral;

        // Server/Header/function.h:92-114 (ReturnTribeRole) -- blocks all three tiers: master, sub-master, or
        // elected vote candidate for the character's own tribe, matching PlayerRuntimeState.TribeRole's own
        // 0-means-no-role encoding. The legacy source also contains a second, vote-active-only re-scan of the
        // same candidate list (S04_MyWork03.cpp:7241-7251) -- confirmed dead weight, not an independent gate:
        // by the time it would run, this check has already proven the character's name matches nothing in
        // that list, so it can never reject anything this didn't already reject. Whether a tribe vote is
        // currently active therefore has no bearing on eligibility at all, and is deliberately not modeled as
        // a separate input here.
        if (ctx.TribeRole != 0)
            return ForcedNeutralTribeResetOutcome.HoldsTribeRole;

        // S04_MyWork03.cpp:7252-7256.
        if (ctx.GuildId is not null || ctx.TeacherCharacterId is not null || ctx.StudentCharacterId is not null)
            return ForcedNeutralTribeResetOutcome.HasGuildOrMentorLink;

        // S04_MyWork03.cpp:7257-7264 (MAX_FRIEND_NUM = 10, DEFINE.h:305).
        if (ctx.HasAnyFriend)
            return ForcedNeutralTribeResetOutcome.HasRegisteredFriends;

        // S04_MyWork03.cpp:7266 -- the neutral faction's designated home zone/server process must currently be
        // connected (a nonzero port in the shared zone-connection table). See
        // Inventory.UseItems.ForcedNeutralTribeResetUseItemHandler's own remarks for why the Fenrir-side
        // resolution of "which map, and is it currently hosted anywhere" is deliberately operator-configured
        // and defaults to permanently offline, rather than a hardcoded map id -- the legacy "server number
        // 140" identification this precondition rests on is only circumstantially corroborated (see the
        // behavior contract's own Edge cases).
        if (!ctx.NeutralHomeZoneOnline)
            return ForcedNeutralTribeResetOutcome.NeutralHomeZoneOffline;

        return ForcedNeutralTribeResetOutcome.Success;
    }
}

/// <summary>
///     Every server-authoritative input <see cref="ForcedNeutralTribeResetGate.Evaluate" /> needs, already
///     resolved by the caller -- never anything the client asserts. <see cref="NeutralHomeZoneOnline" /> is
///     the one input that requires I/O to resolve (a live cross-shard directory read), which is why it is
///     folded into this otherwise-pure context rather than being read from inside the gate itself.
/// </summary>
public readonly record struct ForcedNeutralTribeResetEligibilityContext(
    short Level,
    byte CurrentTribe,
    byte TribeRole,
    int? GuildId,
    int? TeacherCharacterId,
    int? StudentCharacterId,
    bool HasAnyFriend,
    bool NeutralHomeZoneOnline);
