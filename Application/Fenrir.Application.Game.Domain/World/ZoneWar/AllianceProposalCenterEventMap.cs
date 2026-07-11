using System.Buffers.Binary;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The A5-alliance-proposal behavior contract's center-side dispatch discriminators 46-49
///     (<c>Server/ts25center/S04_MyWork02.cpp:212</c> switch, cases at <c>:408-472</c>) -- the accept/
///     reject-slot state machine over <see cref="AllianceProposalCenterState" />'s <c>mAllianceState</c>/
///     <c>mPossibleAllianceInfo</c> fields. Same shape as <see cref="TribeBonusRatioEventMap" />: a pure,
///     directly-testable <see cref="Apply" /> entry point a future <see cref="ZoneCenterBroadcastIngestor" />
///     wiring pass calls from its own <c>ApplyStateEffect</c> range table -- see this slice's own reported
///     wiring manifest for the exact snippet, not applied here (shared-file collision avoidance).
///     <para>
///         Discriminator 46 ("finalize/accept a brand-new alliance") is <b>dead code</b>: no sender anywhere in
///         <c>Server/</c> was found to ever transmit it (the ritual's own new-alliance-negotiation success path,
///         <c>S07_MyGame01.cpp:3959-3961</c>, jumps straight to its post-ritual lockout state with no send at
///         all). <see cref="ApplyFinalizeNewAlliance" /> reproduces the legacy dispatch case's own would-be
///         mutation faithfully anyway (structural completeness with the legacy switch, and so a future feature
///         that DOES want to let two tribes actually finalize an alliance has an exact, tested starting point) --
///         but per the source contract's own explicit flag, wiring case 46 into a live dispatch table (or
///         inventing a Fenrir-side sender for it) is NOT a legacy-parity requirement, it would be new product
///         design. Do not treat its presence here as evidence the feature is "supported."
///     </para>
///     <para>
///         Discriminator 48 ("an alliance-stone monster is under attack") is a bare no-op on the center side
///         (<c>S04_MyWork02.cpp:449</c>) -- <see cref="Apply" /> has no mutation for it whatsoever, matching
///         Zone049 sub-code 1's own "recognized but zero state effect" precedent in
///         <see cref="ZoneCenterBroadcastIngestor" />.
///     </para>
///     <para>
///         Discriminators 47 ("break via ritual") and 49 ("break via stone-capture") share byte-for-byte
///         identical mutation logic (<see cref="ApplyBreakAlliance" />) -- the source contract confirms this
///         explicitly; the only difference between them is which zone-side event produced the send and the
///         cooldown-length the SENDER computed into the two date fields before transmitting (14 days for 47,
///         28 for 49) -- this dispatch layer itself is agnostic to that length, it only stores whatever dates
///         arrived.
///     </para>
///     <para>
///         Tribe-to-date pairing (inference, not a literal contract statement): the payload's four whole-number
///         fields are laid out tribeA, tribeB, dateA, dateB in that order (offsets 0/4/8/12,
///         <c>S04_MyWork02.cpp:409-410,428-431,451-454</c>); this class pairs tribeA with dateA and tribeB with
///         dateB on the strength of that shared field ordering. The source contract never states the pairing in
///         those exact words -- flagged as an explicit open question rather than silently assumed watertight.
///     </para>
///     <para>
///         Bounds-checking (Fenrir-side hardening the legacy dispatch handler itself does not perform, per the
///         source contract's own Edge cases section): an out-of-range tribe id on either field causes the whole
///         event to be logged and ignored, mutating nothing -- matching every other numbered family
///         <see cref="ZoneCenterBroadcastIngestor" /> already guards the same way.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25center/S04_MyWork02.cpp:212 (discriminator switch) ; :408-426 (case 46) ;
///     :427-448 (case 47) ; :449 (case 48, bare no-op) ; :450-472 (case 49) ; Server/Header/Protocol/ZONE.h:19-24
///     (fixed 130-byte payload shape) ; Server/ts25zone/S07_MyGame01.cpp:3959-3961 (new-alliance-negotiation
///     success path, confirms no case-46 send exists) ; Server/ts25zone/S07_MyGame01.cpp:3997-3999 (case 47's
///     sender, 14-day dates) ; Server/ts25zone/S07_MyGame05.cpp:1643-1653 (case 49's sender, 28-day dates).
/// </remarks>
public static class AllianceProposalCenterEventMap
{
    /// <summary>Case 46 -- dead code, see class remarks. Never sent by any live legacy or Fenrir path.</summary>
    public const int FinalizeNewAllianceEventCode = 46;

    /// <summary>Case 47 -- break an existing alliance via the ritual's already-allied-negotiation countdown.</summary>
    public const int BreakAllianceViaRitualEventCode = 47;

    /// <summary>Case 48 -- informational only, zero state mutation. See class remarks.</summary>
    public const int AllianceStoneUnderAttackEventCode = 48;

    /// <summary>Case 49 -- break an existing alliance via alliance-stone capture. Identical mutation to 47.</summary>
    public const int BreakAllianceViaStoneCaptureEventCode = 49;

    /// <summary>Contiguous 46-49 range this whole sub-mechanic occupies in the shared dispatch switch.</summary>
    public const int EventCodeRangeStart = FinalizeNewAllianceEventCode;

    public const int EventCodeRangeEnd = BreakAllianceViaStoneCaptureEventCode;

    /// <summary>
    ///     Decode and apply one of the four discriminator-46-49 events against <paramref name="state" />. Never
    ///     throws for an out-of-range event code outside 46-49 (silently does nothing, matching the "everything
    ///     else falls through with zero state effect" convention <see cref="ZoneCenterBroadcastIngestor" />
    ///     documents for its own range table) -- callers are expected to range-check before calling, the same
    ///     way every existing family in that class's own <c>ApplyStateEffect</c> switch already does.
    /// </summary>
    /// <param name="eventCode">One of <see cref="FinalizeNewAllianceEventCode" />/<see cref="BreakAllianceViaRitualEventCode" />/<see cref="AllianceStoneUnderAttackEventCode" />/<see cref="BreakAllianceViaStoneCaptureEventCode" />.</param>
    /// <param name="data">The raw 130-byte broadcast payload (<see cref="ZoneCenterBroadcastIngestor.PayloadSize" />).</param>
    /// <param name="state">The process-wide alliance-proposal mirror to mutate.</param>
    /// <param name="logger">Used to record an out-of-range tribe id -- see class remarks, Bounds-checking.</param>
    public static void Apply(int eventCode, ReadOnlySpan<byte> data, AllianceProposalCenterState state,
        ILogger logger)
    {
        switch (eventCode)
        {
            case FinalizeNewAllianceEventCode:
                ApplyFinalizeNewAlliance(data, state, logger);
                break;

            case BreakAllianceViaRitualEventCode:
            case BreakAllianceViaStoneCaptureEventCode:
                ApplyBreakAlliance(eventCode, data, state, logger);
                break;

            case AllianceStoneUnderAttackEventCode:
                // Bare no-op on the center side (S04_MyWork02.cpp:449) -- nothing to mutate.
                break;
        }
    }

    /// <summary>
    ///     Case 46 (S04_MyWork02.cpp:408-426): selects slot 0 only if it is currently empty, else unconditionally
    ///     selects slot 1 with no independent check that slot 1 is actually free; clears both tribes' possible-
    ///     alliance cooldown markers to zero; writes the pair into the chosen slot -- all as one atomic unit via
    ///     <see cref="AllianceProposalCenterState.ApplyFinalize" /> (see that method's own remarks for why this
    ///     is not three separate calls).
    /// </summary>
    private static void ApplyFinalizeNewAlliance(ReadOnlySpan<byte> data, AllianceProposalCenterState state,
        ILogger logger)
    {
        var tribeA = ReadInt32(data, 0);
        var tribeB = ReadInt32(data, 4);

        if (!TryValidateTribes(tribeA, tribeB, FinalizeNewAllianceEventCode, logger))
            return;

        state.ApplyFinalize((byte)tribeA, (byte)tribeB);
    }

    /// <summary>
    ///     Cases 47/49 (S04_MyWork02.cpp:427-448,450-472): selects slot 0 only if it already holds exactly this
    ///     tribe pair (either order), else unconditionally selects slot 1 with no independent verification;
    ///     arms each tribe's possible-alliance cooldown marker to its own sent expiry date; clears the selected
    ///     slot back to empty -- all as one atomic unit via <see cref="AllianceProposalCenterState.ApplyBreak" />.
    /// </summary>
    private static void ApplyBreakAlliance(int eventCode, ReadOnlySpan<byte> data, AllianceProposalCenterState state,
        ILogger logger)
    {
        var tribeA = ReadInt32(data, 0);
        var tribeB = ReadInt32(data, 4);
        var expiryA = ReadInt32(data, 8);
        var expiryB = ReadInt32(data, 12);

        if (!TryValidateTribes(tribeA, tribeB, eventCode, logger))
            return;

        state.ApplyBreak((byte)tribeA, (byte)tribeB, expiryA, expiryB);
    }

    private static bool TryValidateTribes(int tribeA, int tribeB, int eventCode, ILogger logger)
    {
        if (AllianceProposalCenterState.IsValidTribe(tribeA) && AllianceProposalCenterState.IsValidTribe(tribeB))
            return true;

        logger.LogWarning(
            "Alliance proposal event {EventCode} referenced out-of-range tribe id(s) {TribeA}/{TribeB} -- ignored",
            eventCode, tribeA, tribeB);
        return false;
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    }
}
