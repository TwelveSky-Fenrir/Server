using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Individually-named <c>tSort</c> event codes for the ts25center <c>ZONE_BROADCAST_FOR_CENTER_SEND</c>
///     family's Zone267 (per-tribe symbol-war state), Zone241 "Den of Rebirth" (state mirror), and
///     pure-relay (no cached state at all) groups -- the A5-zone175-literals behavior contract's own scope.
///     Before this catalog, every one of these codes existed only as a bare integer: a range constant
///     (<see cref="ZoneCenterBroadcastIngestor.Zone267RangeStart" />/<c>RangeEnd</c>,
///     <see cref="ZoneCenterBroadcastIngestor.Zone241RangeStart" />/<c>RangeEnd</c>) covering the whole
///     span with no per-code identity, or a bare number in the "Everything else (416-428, 500, ...)"
///     fallthrough comment inside <see cref="ZoneCenterBroadcastIngestor.ApplyStateEffect" /> -- this class
///     gives each one a symbolic name and, where the cited source carries one, its verbatim legacy label.
///     <para>
///         <b>Zone175 (event codes 64-100, reset 110) is deliberately NOT duplicated here.</b>
///         <see cref="SiegeEventStateMap.TryMapZone175" /> already is that family's byte-exact code-&gt;state
///         table, and the contract's own Edge-cases/Open-questions section is explicit that none of Zone175's
///         22 individual state values (nor the shared "23" collapse value) has any real-world name recoverable
///         from the cited source -- the only fact known about each Zone175 code is which of the 23 states it
///         writes, which <see cref="SiegeEventStateMap" /> already encodes precisely. Adding a
///         <c>Zone175State1EventCode = 64</c>-style alias here would restate that same switch a second time
///         with no new information and a real risk of the two drifting apart; callers that need Zone175's
///         code-&gt;state mapping should call <see cref="SiegeEventStateMap.TryMapZone175" /> directly.
///     </para>
///     <para>
///         Zone267 codes likewise have no individual legacy label in the cited source -- only the aggregate
///         "which state value does this code write" fact is known (see
///         <see cref="SiegeEventStateMap.TryMapZone267" />), so the Zone267 constants below are named after
///         the state each one writes, the only fact available, not a guessed real-world meaning.
///     </para>
///     <para>
///         Zone241 and the pure-relay group DO carry verbatim legacy label text in the cited source (comments
///         sitting next to each <c>case</c> label) -- those constants' XML docs quote that text exactly,
///         including the source's own typo ("falied") and the two duplicated-comment pairs (Zone241 412/413,
///         relay-group 422/424) the contract flags as unable to be told apart after the fact.
///     </para>
///     <para>
///         <b>Event code 417 is deliberately absent from this catalog.</b> No case label, comment, or send
///         site for 417 exists anywhere in the cited source -- it is a genuine, unrecovered gap, not an
///         oversight here. Under the universal "unmatched code" rule (see
///         <see cref="ZoneCenterBroadcastIngestor.Ingest" />'s own no-`default:`-needed relay), 417 already
///         behaves correctly today (no state write, still relayed) purely because no <c>case</c> exists for
///         it anywhere in <see cref="ZoneCenterBroadcastIngestor.ApplyStateEffect" /> -- adding a named
///         constant for a code whose meaning is genuinely unknown would misrepresent it as understood. See
///         <see cref="KnownGapEventCodes" />.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ (via the A5-zone175-literals behavior contract, itself citing):
///     Server/ts25center/S04_MyWork02.cpp:929-961 (Zone267, cases 402-410) ; :962-986 (Zone241, cases
///     411-415, including each case's own verbatim trailing comment) ; :987-1012 (the pure-relay range
///     416-428 and 500, including the absent case 417, the blank comment on 423, and the duplicated "FoL
///     success" comment shared by 422 and 424) ; :1213-1221 (the unconditional post-switch relay every code
///     above -- matched or not -- always reaches). "FoL" itself is never expanded anywhere in the cited
///     source.
/// </remarks>
public static class SiegeZoneLiteralEventCatalog
{
    // ---- Zone267 (S04_MyWork02.cpp:929-961) -- named after the state value each code writes, the only
    // fact recoverable from the cited source; see SiegeEventStateMap.TryMapZone267 for the authoritative
    // numeric mapping this class does not duplicate.

    /// <summary>Writes nothing (<see cref="SiegeEventStateMap.TryMapZone267" /> returns <see langword="false" />).</summary>
    public const int Zone267NoOpEventCode = 402;

    /// <summary>Writes Zone267 state 1.</summary>
    public const int Zone267WritesState1EventCode = 403;

    /// <summary>Writes Zone267 state 2.</summary>
    public const int Zone267WritesState2EventCode = 404;

    /// <summary>Writes Zone267 state 3.</summary>
    public const int Zone267WritesState3EventCode = 405;

    /// <summary>Writes Zone267 state 5 (first of three codes -- 406/407/409 -- that all write the same state 5).</summary>
    public const int Zone267WritesState5EventCodeA = 406;

    /// <summary>Writes Zone267 state 5 (second of three -- see <see cref="Zone267WritesState5EventCodeA" />).</summary>
    public const int Zone267WritesState5EventCodeB = 407;

    /// <summary>Writes Zone267 state 4.</summary>
    public const int Zone267WritesState4EventCode = 408;

    /// <summary>Writes Zone267 state 5 (third of three -- see <see cref="Zone267WritesState5EventCodeA" />).</summary>
    public const int Zone267WritesState5EventCodeC = 409;

    /// <summary>Resets Zone267 to state 0 -- a real write, distinct from the 402 no-op.</summary>
    public const int Zone267ResetEventCode = 410;

    // ---- Zone241 "Den of Rebirth" (S04_MyWork02.cpp:962-986) -- each code DOES carry a verbatim legacy
    // label; see SiegeEventStateMap.TryMapZone241 for the authoritative numeric mapping this class does not
    // duplicate.

    /// <summary>Legacy label (verbatim): "Den of Rebirth Challenge 0 - 11 // 0 // Name". Maps to <see cref="DenOfRebirthChallengeState.ChallengeStarted" />.</summary>
    public const int Zone241ChallengeStartedEventCode = 411;

    /// <summary>
    ///     Legacy label (verbatim): "Den of Rebirth Failure 0 - 11 // 0 // Name". Maps to
    ///     <see cref="DenOfRebirthChallengeState.Ended" /> -- collapses with <see cref="Zone241FailureEventCodeB" />
    ///     and <see cref="Zone241SuccessEventCode" />; the stored state cannot distinguish a win from a loss.
    /// </summary>
    public const int Zone241FailureEventCodeA = 412;

    /// <summary>
    ///     Legacy label is an identical duplicate of <see cref="Zone241FailureEventCodeA" />'s text in the
    ///     cited source ("Den of Rebirth Failure 0 - 11 // 0 // Name"). Maps to
    ///     <see cref="DenOfRebirthChallengeState.Ended" />.
    /// </summary>
    public const int Zone241FailureEventCodeB = 413;

    /// <summary>
    ///     Legacy label (verbatim): "Den of Rebirth Success 0 - 11 // 0 // Name". Maps to
    ///     <see cref="DenOfRebirthChallengeState.Ended" /> -- despite the distinct "Success" label, the stored
    ///     state is identical to the two failure codes above; only the live-relayed event code (never the
    ///     cache) preserves the win/loss distinction.
    /// </summary>
    public const int Zone241SuccessEventCode = 414;

    /// <summary>Legacy label (verbatim): "Den of Rebirth Return Town 0 - 11 //". Resets to <see cref="DenOfRebirthChallengeState.Idle" />.</summary>
    public const int Zone241ReturnTownEventCode = 415;

    // ---- Pure relay -- no cached state written for any of these; their entire effect is the universal
    // relay every code (matched or not) already receives unconditionally in ZoneCenterBroadcastIngestor.Ingest.
    // (S04_MyWork02.cpp:987-1012.)

    /// <summary>Legacy label (verbatim): "The War has started".</summary>
    public const int WarHasStartedEventCode = 416;

    // 417 intentionally has NO named constant here -- see this class's own remarks and KnownGapEventCodes.

    /// <summary>Legacy label (verbatim): "Instinct defense formation in use".</summary>
    public const int InstinctDefenseFormationInUseEventCode = 418;

    /// <summary>Legacy label (verbatim): "return to original faction".</summary>
    public const int ReturnToOriginalFactionEventCode = 419;

    /// <summary>Legacy label (verbatim): "remain to FoL begin".</summary>
    public const int RemainToFolBeginEventCode = 420;

    /// <summary>Legacy label (verbatim): "FoL began".</summary>
    public const int FolBeganEventCode = 421;

    /// <summary>
    ///     Legacy label (verbatim): "FoL success". Text is duplicated verbatim by
    ///     <see cref="FolSuccessDuplicateLabelEventCode" /> (424) in the cited source -- the contract flags this
    ///     as a plausible "failure counterpart to 422" but the source itself gives no confirming text, so no
    ///     corrected guess is encoded here.
    /// </summary>
    public const int FolSuccessEventCode = 422;

    /// <summary>No descriptive text at all exists for this code in the cited source -- its trailing comment is blank.</summary>
    public const int UnlabeledEventCode423 = 423;

    /// <summary>
    ///     Legacy label is a verbatim duplicate of <see cref="FolSuccessEventCode" />'s text ("FoL success") --
    ///     see that constant's own remarks; the true intended label is not recoverable from the cited source.
    /// </summary>
    public const int FolSuccessDuplicateLabelEventCode = 424;

    /// <summary>Legacy label (verbatim): "remain to FoL annhi".</summary>
    public const int RemainToFolAnnihilationEventCode = 425;

    /// <summary>Legacy label (verbatim): "FoL annhi began".</summary>
    public const int FolAnnihilationBeganEventCode = 426;

    /// <summary>Legacy label (verbatim): "FoL annhi succeed".</summary>
    public const int FolAnnihilationSucceedEventCode = 427;

    /// <summary>Legacy label (verbatim, including the source's own typo): "FoL annhi falied" [sic, for "failed"].</summary>
    public const int FolAnnihilationFailedEventCode = 428;

    /// <summary>Legacy label (verbatim): "all factions alliance revoked".</summary>
    public const int AllFactionsAllianceRevokedEventCode = 500;

    /// <summary>
    ///     Every event code in this catalog's pure-relay group -- deliberately excludes 417 (see this class's
    ///     own remarks and <see cref="KnownGapEventCodes" />). No behavior in
    ///     <see cref="ZoneCenterBroadcastIngestor" /> needs to consult this set today (an unmatched
    ///     <c>ApplyStateEffect</c> switch arm already produces the correct "no write, still relay" outcome for
    ///     every one of these codes with no case at all) -- it exists for diagnostics/logging call sites that
    ///     want to recognize "this is a known pure-relay code" without hardcoding the range a second time.
    /// </summary>
    public static readonly FrozenSet<int> PureRelayEventCodes = new[]
    {
        WarHasStartedEventCode, InstinctDefenseFormationInUseEventCode, ReturnToOriginalFactionEventCode,
        RemainToFolBeginEventCode, FolBeganEventCode, FolSuccessEventCode, UnlabeledEventCode423,
        FolSuccessDuplicateLabelEventCode, RemainToFolAnnihilationEventCode, FolAnnihilationBeganEventCode,
        FolAnnihilationSucceedEventCode, FolAnnihilationFailedEventCode, AllFactionsAllianceRevokedEventCode
    }.ToFrozenSet();

    /// <summary>
    ///     Event codes inside this contract's overall numbered ranges that are genuinely unrecoverable from
    ///     the cited source -- currently just 417 (no case, no comment, no send site found anywhere in the
    ///     zone executable). Preserved as data, not silently omitted, so a future re-verification pass has an
    ///     explicit marker of what is still open rather than an absence that looks like an oversight.
    /// </summary>
    public static readonly FrozenSet<int> KnownGapEventCodes = new[] { 417 }.ToFrozenSet();

    /// <summary>
    ///     Best-effort verbatim legacy label lookup for logging/diagnostics, covering every code this catalog
    ///     names (Zone241 and the pure-relay group only -- Zone267 codes have no descriptive label, only a
    ///     state number, see this class's own remarks). Returns <see langword="false" /> for any code this
    ///     catalog does not carry a legacy label for, including 417 and every Zone175/Zone267/Zone049/Zone335
    ///     code (those are named/numbered but never had descriptive prose in the cited source).
    /// </summary>
    public static bool TryGetLegacyLabel(int eventCode, out string label)
    {
        switch (eventCode)
        {
            case Zone241ChallengeStartedEventCode:
                label = "Den of Rebirth Challenge 0 - 11 // 0 // Name";
                return true;
            case Zone241FailureEventCodeA:
            case Zone241FailureEventCodeB:
                label = "Den of Rebirth Failure 0 - 11 // 0 // Name";
                return true;
            case Zone241SuccessEventCode:
                label = "Den of Rebirth Success 0 - 11 // 0 // Name";
                return true;
            case Zone241ReturnTownEventCode:
                label = "Den of Rebirth Return Town 0 - 11 //";
                return true;
            case WarHasStartedEventCode:
                label = "The War has started";
                return true;
            case InstinctDefenseFormationInUseEventCode:
                label = "Instinct defense formation in use";
                return true;
            case ReturnToOriginalFactionEventCode:
                label = "return to original faction";
                return true;
            case RemainToFolBeginEventCode:
                label = "remain to FoL begin";
                return true;
            case FolBeganEventCode:
                label = "FoL began";
                return true;
            case FolSuccessEventCode:
            case FolSuccessDuplicateLabelEventCode:
                label = "FoL success";
                return true;
            case RemainToFolAnnihilationEventCode:
                label = "remain to FoL annhi";
                return true;
            case FolAnnihilationBeganEventCode:
                label = "FoL annhi began";
                return true;
            case FolAnnihilationSucceedEventCode:
                label = "FoL annhi succeed";
                return true;
            case FolAnnihilationFailedEventCode:
                label = "FoL annhi falied"; // [sic] -- verbatim source typo for "failed"
                return true;
            case AllFactionsAllianceRevokedEventCode:
                label = "all factions alliance revoked";
                return true;
            default:
                label = string.Empty;
                return false;
        }
    }
}
