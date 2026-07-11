using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World.WorldState;

/// <summary>
///     A5-worldinfo-boot-reset: the Fenrir-side reproduction of legacy ts25center's own boot-time WORLD_INFO
///     reset (Server/ts25center/S07_MyGame01.cpp:7-193) for exactly six requested field groups -- votes,
///     close-vote, alliance-possibility, DTM, Nok-San, and popup -- applied onto whatever <see cref="WorldInfo" />
///     wire template it is handed.
///     <para>
///         The single most consequential fact the source behavior contract establishes (its own Edge Cases
///         section): legacy's boot reset is a CURATED, field-by-field reset, never a whole-region clear -- every
///         field not explicitly named is left completely untouched, whatever it already held. <see cref="Apply" />
///         reproduces exactly that shape via a targeted <c>record with</c> update: only the properties this class
///         documents below ever change, every other <see cref="WorldInfo" /> field on the input template survives
///         byte-for-byte.
///     </para>
///     <para>
///         Legacy's own reset order (contract Side effects, steps 1-7) runs entirely before its own database
///         connection is even attempted (step 8) and needs no I/O -- this reproduction is likewise a pure,
///         synchronous, allocation-only transform with no I/O of its own, safe to run as the very first thing in
///         Fenrir's own boot-preload sequence.
///     </para>
///     <para>
///         What this class deliberately does NOT do: legacy's own step 9 (contract Side effects) selectively
///         overlays a PERSISTED database row on top of this reset, but only for votes/close-vote/
///         alliance-possibility -- never for DTM/Nok-San/popup, which stay at their compiled-in zero for the rest
///         of the process's life until a live gameplay write changes them (contract Edge Cases: "DTM, Noksan, and
///         Popup are exclusively owned by live writes from ts25zone... with no ts25center-side persistence at
///         all"). Fenrir's own schema has no backing SQL column for votes/close-vote/alliance-possibility today
///         (see <see cref="ZoneWar.TribeVoteElection" />'s own GAP remarks for votes, and
///         <see cref="ZoneWar.AllianceProposalCenterState" />'s own "not persisted" remarks for
///         alliance-possibility, which -- as of the sibling A5-alliance-proposal slice -- now has real
///         PROCESS-LOCAL backing domain state, just no durable one) -- so there is currently nothing to
///         selectively overlay from at the wire-template level this class operates on, and this class implements
///         only the compiled-in reset half (steps 1-7), never inventing a step-9 overlay Fenrir cannot yet back
///         with real persisted data. When that schema exists, the overlay belongs in a NEW step layered on top of
///         this one's output, not folded into this method.
///     </para>
///     <para>
///         Alliance-possibility now has real backing domain state to reset at the process level (not just this
///         wire-template level): see <see cref="ZoneWar.AllianceProposalCenterState" /> (the sibling
///         A5-alliance-proposal slice) and <see cref="WorldInfoBootResetVerifier" /> (this same task), which
///         verifies that state's own boot-reset value directly rather than through this wire-level helper --
///         this class's own <c>PossibleAllianceInfo</c>/<c>AllianceState</c> handling below still matters for
///         any wire template built WITHOUT first running it through a live projection of that state (there is
///         no such projection wired yet -- seen this task's own openQuestions).
///     </para>
/// </summary>
public static class WorldInfoBootReset
{
    /// <summary>
    ///     Alliance-possibility (proposal side) carries two values per tribe -- a proposal-timestamp value and a
    ///     pending flag (contract Side effects step 3, S07_MyGame01.cpp:36-43) -- eight values total across
    ///     <see cref="WorldStateService.TribeCount" /> tribes.
    /// </summary>
    private const int PossibleAllianceValuesPerTribe = 2;

    /// <summary>
    ///     Alliance-possibility (formed-slot side) holds two alliance slots (contract Side effects step 4,
    ///     S07_MyGame01.cpp:44-47), each recording a pair of tribe identifiers.
    /// </summary>
    private const int AllianceSlotCount = 2;

    private const int TribeIdsPerAllianceSlot = 2;

    private static readonly int TribeCount = WorldStateService.TribeCount;

    /// <summary>Five popup event types, in the legacy's own fixed enumerated order (<see cref="PopupEventType" />).</summary>
    private static readonly int PopupTypeCount = Enum.GetValues<PopupEventType>().Length;

    /// <summary>
    ///     The canonical boot-reset baseline: <see cref="WorldStateTemplates.ZeroedWorldInfo" /> run through
    ///     <see cref="Apply(WorldInfo, int?)" /> with no alliance-sentinel override (see that overload's own
    ///     remarks on why <c>AllianceState</c> is left exactly as the input template already carries it). Computed
    ///     once, at first touch of this type -- see this task's wiring-manifest notes for where to force that
    ///     touch during Fenrir's own boot-preload sequence, matching legacy's own "unconditional part of the
    ///     executable's own startup sequence" framing (contract Trigger) rather than leaving it to an implicit,
    ///     unforced first use.
    /// </summary>
    public static readonly WorldInfo ZeroedTemplate = Apply(WorldStateTemplates.ZeroedWorldInfo);

    /// <summary>
    ///     Applies the curated six-field-group boot reset onto <paramref name="template" />, returning a new
    ///     <see cref="WorldInfo" /> value -- every field this method does not name below is copied through from
    ///     <paramref name="template" /> completely unchanged (a targeted <c>record with</c>, never a blanket
    ///     reconstruction).
    /// </summary>
    /// <param name="template">The starting <see cref="WorldInfo" /> value to apply the reset onto.</param>
    /// <param name="allianceEmptySlotSentinel">
    ///     The wire encoding legacy uses for an "empty" formed-alliance slot (contract Side effects step 4:
    ///     "both values in both slots are set to a sentinel meaning 'empty'"). The source contract does not give
    ///     this constant's actual value -- it is flagged there as recoverable only by re-opening
    ///     <c>Server/Header/Protocol/STRUCT.h</c>/<c>Server/ts25center/S07_MyGame01.cpp:44-47</c> directly (open
    ///     item, not resolved this session). Because a formed-alliance slot's two recorded values are themselves
    ///     tribe identifiers (0-3, <see cref="WorldStateService.TribeCount" />-bounded), silently defaulting this
    ///     parameter to 0 -- the way every OTHER field in this method safely can, since 0 genuinely means "zero
    ///     count"/"zero timestamp" for all of them -- would risk being flatly wrong rather than merely imprecise
    ///     for this one field, because 0 is itself a live tribe id. This method therefore never guesses it:
    ///     leave this <see langword="null" /> (the default) to pass <paramref name="template" />'s own
    ///     <c>AllianceState</c> through completely untouched, or supply the real sentinel once it is recovered.
    /// </param>
    public static WorldInfo Apply(WorldInfo template, int? allianceEmptySlotSentinel = null)
    {
        var result = template with
        {
            // Votes (contract Side effects step 1, S07_MyGame01.cpp:48-51): zero for every tribe.
            TribeVoteState = new int[TribeCount],

            // Close-vote (contract Side effects step 2, S07_MyGame01.cpp:52-55): zero for every tribe. The
            // contract's own Edge Cases note no live legacy mutation site was found anywhere in Server/ for
            // this field beyond this reset and its own periodic six-tick DB round-trip -- reproduced here for
            // boot-reset parity only, not because Fenrir has (or needs) a live writer for it yet.
            CloseVoteState = new int[TribeCount],

            // Alliance-possibility, proposal side (contract Side effects step 3, S07_MyGame01.cpp:36-43): both
            // values (a proposal-timestamp and a pending flag) zero for every tribe -- eight values total.
            PossibleAllianceInfo = new int[TribeCount * PossibleAllianceValuesPerTribe],

            // DTM (contract Side effects step 5, S07_MyGame01.cpp:131-134): zero for every tribe. Fenrir's own
            // ZoneCenterSiegeState already keeps this live and zero-initialized at construction (see
            // ZoneCenterSiegeProjection, which already projects it onto this same field from live state) -- this
            // reset only matters for a template that has not yet been run through that live projection.
            Zone038DTMValue = new int[TribeCount],

            // Nok-San (contract Side effects step 6, S07_MyGame01.cpp:127-130,141-144): zero every per-tribe
            // stone count and every stone-state slot. Mirrors Zone195NokSanState's own boot-zero backing arrays.
            TribeNokSanStone = new int[TribeCount],
            NokSanStoneState = new int[Zone195NokSanState.StoneSlotCount],

            // Popup (contract Side effects step 7, S07_MyGame01.cpp:135-140): all three per-type arrays zero,
            // for all five popup types, in the legacy's own fixed enumerated order. Mirrors PopupEventState's
            // own boot-false backing array for the type-enabled flags. The two COUNTER groups this legacy field
            // pair models (mPopUpKillAvt/mPopUpKillMonster, one process-wide counter per popup type) have no
            // equivalently-shaped Fenrir backing object at all: Simulation.PopupEventRewardSystem already tracks
            // kill-streak counters, but per-CHARACTER (a ConditionalWeakTable<PlayerRuntimeState, PopupCounters>,
            // a deliberate prior-workstream divergence, not an oversight here) rather than as one shared
            // per-type total the way this wire field pair is shaped -- flagged so nobody mistakes this reset's
            // zeroing of the WIRE field pair for also resetting that unrelated, differently-shaped counter store.
            PopUpTypeState = new int[PopupTypeCount],
            PopUpKillAvt = new int[PopupTypeCount],
            PopUpKillMonster = new int[PopupTypeCount]
        };

        if (allianceEmptySlotSentinel is { } sentinel)
        {
            var allianceState = new int[AllianceSlotCount * TribeIdsPerAllianceSlot];
            Array.Fill(allianceState, sentinel);
            result = result with { AllianceState = allianceState };
        }

        return result;
    }
}
