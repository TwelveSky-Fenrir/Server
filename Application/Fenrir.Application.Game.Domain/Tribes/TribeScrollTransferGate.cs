namespace Fenrir.Application.Game.Domain.Tribes;

/// <summary>
///     Every character-specific, server-authoritative precondition for using catalog items 8153/8154 (the
///     Faction Transfer scroll -- TChangeTribe), pure and I/O-free -- the caller resolves every input
///     (character facts already loaded on <see cref="World.PlayerRuntimeState" />, the client-supplied
///     destination tribe, plus the one cross-shard reachability fact) before calling <see cref="Evaluate" />.
///     <para>
///         Deliberately NOT the same mechanism as <see cref="TribeMigrationGate" /> (CZ_CHANGE_TO_TRIBE4_SEND,
///         op37) or <see cref="ForcedNeutralTribeResetGate" /> (item 8100): this scroll converts between the
///         three PLAYABLE tribes (0-2), client-chooses the destination, and performs a full best-effort
///         equipment/skill/hotkey remap on success -- neither sibling mechanism does all three of those. The
///         three mechanisms happen to share several gate shapes (tribe-role, guild/mentor, friend list) because
///         all three ultimately consult the same legacy <c>ReturnTribeRole</c>/<c>aFriend</c>/<c>aTeacher</c>/
///         <c>aStudent</c> fields -- convergent reuse of the same character facts, not evidence of one gate
///         wearing three opcodes.
///     </para>
/// </summary>
/// <remarks>
///     Réf. "Faction Transfer scroll (items 8153/8154) runtime gates + TChangeTribe mutation" behavior contract
///     (legacy-behavior-translator), itself citing Server/ts25zone/S04_MyWork03.cpp:4740-4841 throughout.
///     <para>
///         <b>Gate 4 (the ts25extra cross-process per-destination-tribe admin toggle) is deliberately NOT
///         modeled anywhere in this type.</b> Legacy blocks a transfer attempt on a synchronous round trip to
///         the separate ts25extra process, which checks a per-destination-tribe on/off toggle an operator can
///         flip at any time (Server/ts25extra/S08_MyDB.cpp:1217-1238, <c>skyinfo.s_tchange0/1/2</c>) -- Fenrir
///         has no ts25extra-equivalent process and no persisted per-tribe transfer-toggle table anywhere in
///         this schema. Per this project's "never invent a gate" rule, no such mechanism is added here; a
///         dedicated fenrir-database-engineer + product decision is needed before this gate can be reproduced
///         (an operator-facing per-tribe on/off switch, most likely a new small table + a
///         <c>GameServerOptions</c>-style default). Flagged as an explicit open item, not silently skipped:
///         search this class for "GATE 4" to find the exact slot it would occupy in <see cref="Evaluate" />.
///     </para>
///     <para>
///         <b>Gate 3's "must equal one exact fixed threshold" reads as an exact-equality check in the
///         originating contract, but is modeled here as "at least" (&lt; 145 rejects).</b> LV_M33 = 145
///         (Server/Header/Protocol/DEFINE.h:483, the SAME constant <see cref="TribeMigrationGate.MinLevel" />
///         independently cites for the unrelated op37 mechanism) is also
///         <see cref="Stats.LevelProgressionCalculator.MaxLevel" /> -- the character's own aLevel1 field can
///         never exceed it. "Must equal 145" and "must be &gt;= 145" are therefore behaviorally IDENTICAL given
///         that hard ceiling; modeled as "&gt;=" purely because that is the idiom every other level-floor gate
///         in this codebase already uses, not a divergence from the cited behavior.
///     </para>
/// </remarks>
public static class TribeScrollTransferGate
{
    /// <summary>LV_M33, Server/Header/Protocol/DEFINE.h:483 -- see this type's own remarks for the exact-equality-vs-floor equivalence.</summary>
    public const short MinLevel = 145;

    public static TribeScrollTransferOutcome Evaluate(TribeScrollTransferEligibilityContext ctx)
    {
        // Gate 1 (S04_MyWork03.cpp:4745-4749) -- hardens legacy's own inert "< 0 AND > 2" bound check into a
        // real one.
        if (!TribeConversionResolver.IsPlayableTribe(ctx.ToTribe))
            return TribeScrollTransferOutcome.InvalidDestinationTribe;

        // Gate 2 (S04_MyWork03.cpp:4750-4754) -- unconditional, no neutral exemption.
        if (ctx.ToTribe == ctx.PreviousTribe)
            return TribeScrollTransferOutcome.AlreadyTargetTribe;

        // Gate 3 (S04_MyWork03.cpp:4756-4763) -- see this type's own remarks for the exact-equality-vs-floor note.
        if (ctx.Level < MinLevel)
            return TribeScrollTransferOutcome.LevelTooLow;

        // GATE 4 (ts25extra per-destination-tribe admin toggle, S04_MyWork03.cpp:4765-4773) -- deliberately NOT
        // modeled. See this type's own remarks.

        // Gate 5 (S04_MyWork03.cpp:4775-4782).
        if (!ctx.HomeZoneOnline)
            return TribeScrollTransferOutcome.HomeZoneOffline;

        // Gate 6 (S04_MyWork03.cpp:4783-4790, mapcheck.h:83-114 IsValidTown).
        if (!IsValidTown(ctx.CurrentTribe, ctx.MapId))
            return TribeScrollTransferOutcome.WrongLocation;

        // Gate 7 (S04_MyWork03.cpp:4791-4798, function.h:92-114 ReturnTribeRole -- all 3 non-zero tiers).
        if (ctx.TribeRole != 0)
            return TribeScrollTransferOutcome.HoldsTribeRole;

        // Gate 8 (S04_MyWork03.cpp:4799-4803) -- "registered as anyone's mentor" means the character HAS a
        // student (PlayerRuntimeState.StudentCharacterId), i.e. is currently acting as someone's teacher.
        if (ctx.StudentCharacterId is not null)
            return TribeScrollTransferOutcome.IsMentor;

        // Gate 9 (S04_MyWork03.cpp:4804-4808) -- "registered as anyone's mentee" means the character HAS a
        // teacher (PlayerRuntimeState.TeacherCharacterId), i.e. is currently someone else's student.
        if (ctx.TeacherCharacterId is not null)
            return TribeScrollTransferOutcome.IsMentee;

        // Gate 10 (S04_MyWork03.cpp:4809-4816).
        if (ctx.InParty)
            return TribeScrollTransferOutcome.InParty;

        // Gate 11 (S04_MyWork03.cpp:4817-4824).
        if (ctx.GuildId is not null)
            return TribeScrollTransferOutcome.HasGuild;

        // Gate 12 (S04_MyWork03.cpp:4825-4832).
        if (ctx.CapeEquipped)
            return TribeScrollTransferOutcome.CapeEquipped;

        // Gate 13 (S04_MyWork03.cpp:4833-4840, MAX_FRIEND_NUM = 10, DEFINE.h:305).
        if (ctx.HasAnyFriend)
            return TribeScrollTransferOutcome.HasRegisteredFriends;

        return TribeScrollTransferOutcome.Success;
    }

    /// <summary>
    ///     IsValidTown: the character's own CURRENT tribe maps to exactly one town/server number -- tribe 0-2
    ///     to zones 1/6/11, tribe 3 (neutral) to zone 140. Duplicated from
    ///     <c>Fenrir.Application.Game.Services.Tribes.TribeActionService.IsValidTown</c> (private, Services
    ///     layer -- Domain cannot depend on Services) rather than shared, the same independent-citation-
    ///     duplication convention this codebase already applies elsewhere (e.g. <c>PetSlots.EquipmentSlot</c>
    ///     vs. its Login-side twin). Server/Header/mapcheck.h:83-114.
    /// </summary>
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

/// <summary>
///     Every server-authoritative input <see cref="TribeScrollTransferGate.Evaluate" /> needs, already resolved
///     by the caller -- never anything the client asserts except <see cref="ToTribe" /> itself (which is why
///     gate 1 exists at all). <see cref="HomeZoneOnline" /> is the one input that requires I/O to resolve (a
///     live cross-shard directory read), which is why it is folded into this otherwise-pure context rather than
///     being read from inside the gate itself.
/// </summary>
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
    bool HomeZoneOnline);
