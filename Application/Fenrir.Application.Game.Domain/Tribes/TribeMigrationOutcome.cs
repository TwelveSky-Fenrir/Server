namespace Fenrir.Application.Game.Domain.Tribes;

/// <summary>
///     Every distinguishable outcome of a CZ_CHANGE_TO_TRIBE4_SEND (op37) attempt. Kept granular here for
///     logging/testability even though the wire only ever exposes three shapes -- <see cref="Success" />
///     (a ZC_CHANGE_TO_TRIBE4_RECV carrying the success code), one of the three
///     <see cref="TribeMigrationOutcomeExtensions.RepliesWithFailure" /> members (the same reply, carrying a
///     generic non-zero failure code), or every other member (the session is torn down instead, no reply
///     packet at all). See <see cref="TribeMigrationOutcomeExtensions.RepliesWithFailure" /> for the mapping.
/// </summary>
/// <remarks>
///     Réf. "Fourth-tribe (Fujin) conversion and return" behavior contract (legacy-behavior-translator),
///     itself citing Server/ts25zone/S04_MyWork02.cpp:7565-7758 throughout. The legacy split -- most
///     rejections silently disconnect the session, only feature-disabled/realm-unreachable/outside-time-
///     window/tribe-1-joining-tribe-3 reply with a generic failure code -- is preserved deliberately (see the
///     contract's own Error/failure semantics section): it is a faithfully-recorded characteristic of a
///     feature that was authored but never shipped, not a design to imitate, but unifying it into one
///     consistent response model would be inventing new wire behavior the contract explicitly leaves open.
///     <para>
///         Two of the legacy's four replying gates -- the zone process's configured server number matching a
///         fixed per-tribe realm (71/72/73/140), and the tribe-4 realm being currently connected -- have no
///         Fenrir equivalent and are not modeled here at all: Fenrir runs one shared, map-sharded world, never
///         one whole game-server instance per tribe (see CLAUDE.md's architecture section and
///         <c>GameServerOptions.VoteTribeEnabled</c>'s own remarks for the same "map-id-hosted-by-this-shard,
///         not server-number" translation applied elsewhere in this codebase). Only three replying gates exist
///         here, not four.
///     </para>
/// </remarks>
public enum TribeMigrationOutcome
{
    Success,

    // ---- Replies with a generic failure ZC_CHANGE_TO_TRIBE4_RECV; session stays connected ----

    /// <summary>The zone-wide "JonNangin" feature toggle is off. Server/ts25zone/S04_MyWork02.cpp:7570-7575.</summary>
    FeatureDisabled,

    /// <summary>
    ///     Outbound (into tribe 3) only: outside the Saturday 16:00-18:59 conversion window.
    ///     Server/ts25zone/S07_MyGame03.cpp:4888-4891.
    /// </summary>
    OutsideConversionWindow,

    /// <summary>Outbound only: tribe 1 is categorically excluded from joining tribe 3. S04_MyWork02.cpp:7646-7651.</summary>
    Tribe1CannotJoinTribeFour,

    // ---- Silently disconnects the session; no reply packet at all ----

    /// <summary>Both branches: primary level below LV_M33 (145). S04_MyWork02.cpp:7628-7632.</summary>
    LevelTooLow,

    /// <summary>Outbound only: the character's own tribe's world-wide point total is below 100. S04_MyWork02.cpp:7652-7656.</summary>
    TribePointsBelowThreshold,

    /// <summary>
    ///     Outbound only: the alliance-adjusted world-state check failed -- either tribe 3 is already at least
    ///     as strong as the strongest combat-tribe bloc, or the character's own tribe is the alliance-adjusted
    ///     weakest of the three. CheckPossibleChangeToTribe4, Server/Header/function.h:3014-3045. The legacy
    ///     function returns two distinct results here; this outcome deliberately does not distinguish them,
    ///     matching the contract's own note that the handler treats both as one undifferentiated block.
    /// </summary>
    NotEligibleByWorldState,

    /// <summary>
    ///     Outbound only: the character's tribe is not the single raw (non-alliance-adjusted) tribe-point
    ///     leader among the three combat tribes. ReturnBigTribe, Server/Header/function.h:3124-3144.
    /// </summary>
    NotDominantTribe,

    /// <summary>
    ///     Return only: the character's origin tribe (<see cref="World.PlayerRuntimeState.PreviousTribe" />)
    ///     is strictly ahead of tribe 3 in world-wide points. S04_MyWork02.cpp:7689-7693.
    /// </summary>
    OriginTribeAheadOfTribeThree,

    /// <summary>
    ///     Return only: no banked <see cref="World.PlayerRuntimeState.TribeFourReturnAllowance" /> charge.
    ///     S04_MyWork02.cpp:7694-7698.
    /// </summary>
    NoReturnAllowance,

    /// <summary>
    ///     Return only, Fenrir-only hardening (not a distinct legacy gate): <see cref="World.PlayerRuntimeState.PreviousTribe" />
    ///     is outside the legal 0-2 range. Never trusted as pre-validated -- see the behavior contract's own
    ///     Edge Cases entry on the legacy's unvalidated tPreviousTribe write path.
    /// </summary>
    InvalidPreviousTribe,

    /// <summary>Both branches: holds a tribe office (master, sub-master, or elected vote candidate). S04_MyWork02.cpp:7701-7705.</summary>
    HoldsTribeRole,

    /// <summary>Both branches: has a guild, a teacher, or a student on record. S04_MyWork02.cpp:7706-7720.</summary>
    HasGuildOrMentorLink,

    /// <summary>Both branches: at least one non-empty friend-list slot. S04_MyWork02.cpp:7721-7728.</summary>
    HasRegisteredFriends,

    /// <summary>
    ///     Both branches, checked LAST (Fenrir-only hardening reordering -- see
    ///     <see cref="Tribes.TribeMigrationGate" />'s own remarks): the shared global conversion quota is
    ///     exhausted. Server/ts25extra/S08_MyDB.cpp:1193-1215 (MyDB::CheckUpdateSky).
    /// </summary>
    QuotaExhausted
}

public static class TribeMigrationOutcomeExtensions
{
    /// <summary>
    ///     True for the three legacy gates that reply with a generic failure ZC_CHANGE_TO_TRIBE4_RECV instead
    ///     of tearing the session down -- see <see cref="TribeMigrationOutcome" />'s own remarks.
    /// </summary>
    public static bool RepliesWithFailure(this TribeMigrationOutcome outcome)
    {
        return outcome is TribeMigrationOutcome.FeatureDisabled or TribeMigrationOutcome.OutsideConversionWindow
            or TribeMigrationOutcome.Tribe1CannotJoinTribeFour;
    }
}
