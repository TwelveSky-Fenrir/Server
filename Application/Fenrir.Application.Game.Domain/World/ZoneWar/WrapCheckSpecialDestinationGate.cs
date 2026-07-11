namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Full <c>MyUtil::WrapCheck</c> replacement: composes the sixteen-zone tribe-corridor group
///     (<see cref="TribeGuardCorridorGate" />) with the three additional destination groups
///     <see cref="WrapCheckSpecialDestinationCatalog" /> models (win-zone-038, rebirth-exact-count, rebirth-at-
///     least-one instanced) into the single evaluation the legacy function's own switch performs in one pass.
///     Every one of these four groups' destination-zone-id sets is mutually exclusive (they are simply
///     different <c>case</c> labels inside the same C++ switch), so at most one group's rule ever applies to a
///     given <paramref name="destinationZoneId" />; anything named by none of them falls through to
///     <see cref="TribeGuardCorridorGate" />'s own unconditional default-allow.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:5721-5943 (<c>WrapCheck</c> in full -- the single switch this
///     class and <see cref="TribeGuardCorridorGate" /> jointly reproduce) ; Server/ts25zone/S04_MyWork02.cpp:
///     2148-2166 (the single caller, <c>DEMAND_ZONE_SERVER_INFO_2</c> -- the GM-rank bypass and the
///     hard-disconnect-vs-soft-failure split around zone 37 are the CALLER's concern for every one of
///     <c>WrapCheck</c>'s internal cases alike, not particular to the tribe-corridor group alone, so this class
///     reuses <see cref="TribeGuardCorridorGate.HardDisconnectZoneId" /> rather than redeclaring it).
///     <see cref="HolyStoneTribeMatch" /> already implements the exact
///     "does this tribe hold, or is allied with the holder of, zone 38 right now" comparison
///     (<c>ReturnWinZone038</c>, Server/ts25zone/S07_MyGame01.cpp:3114-3117) the win-zone-038 group needs --
///     reused here rather than re-implemented, and its own null-holder-never-matches semantics (nobody has ever
///     captured zone 38 yet) is preserved unchanged.
/// </remarks>
public static class WrapCheckSpecialDestinationGate
{
    /// <param name="requesterTribe">The mover's own tribe (0-3).</param>
    /// <param name="originZoneId">The zone the mover's connection is currently hosted on.</param>
    /// <param name="destinationZoneId">The zone number being requested.</param>
    /// <param name="requesterIsGmOrAdminRank">
    ///     True for any account rank above ordinary-player -- every one of <c>WrapCheck</c>'s cases is skipped
    ///     for a GM mover, not just the tribe-corridor group (Server/ts25zone/S04_MyWork02.cpp:2148).
    /// </param>
    /// <param name="moverRebirthCount">
    ///     The mover's own rebirth count. Only consulted by the rebirth-exact-count and instanced groups; no
    ///     range is enforced by the legacy switch itself beyond the exact-match/at-least-one tests below.
    /// </param>
    /// <param name="zone38WinningTribe">
    ///     The tribe that currently holds zone 38, or null if it has never been captured
    ///     (<see cref="WorldState.WorldStateService" />'s own <c>Zone038WinTribe</c>) -- forwarded verbatim to
    ///     <see cref="HolyStoneTribeMatch.Matches" />, which never matches a null holder.
    /// </param>
    /// <param name="corridorCatalog">
    ///     The tribe-corridor shape, forwarded to <see cref="TribeGuardCorridorGate" /> for the
    ///     fall-through case.
    /// </param>
    /// <param name="corridorState">The live tribe-corridor open/closed table, forwarded the same way.</param>
    /// <param name="resolveAllyOfOwnerTribe">
    ///     Resolves a tribe's own declared ally. Called with <paramref name="zone38WinningTribe" /> for the
    ///     win-zone-038 group (never with <paramref name="requesterTribe" />, matching
    ///     <see cref="HolyStoneTribeMatch" />'s own calling convention), and forwarded
    ///     verbatim to <see cref="TribeGuardCorridorGate" /> for the fall-through case.
    /// </param>
    public static TribeGuardCorridorMoveOutcome Evaluate(
        TribeGuardCorridorCatalog corridorCatalog,
        TribeGuardCorridorState corridorState,
        byte requesterTribe,
        short originZoneId,
        short destinationZoneId,
        bool requesterIsGmOrAdminRank,
        int moverRebirthCount,
        byte? zone38WinningTribe,
        Func<byte, byte?>? resolveAllyOfOwnerTribe = null)
    {
        if (requesterIsGmOrAdminRank)
            return TribeGuardCorridorMoveOutcome.Allowed;

        if (WrapCheckSpecialDestinationCatalog.IsWinZone038Destination(destinationZoneId))
        {
            var allyOfWinner = zone38WinningTribe is { } winner ? resolveAllyOfOwnerTribe?.Invoke(winner) : null;
            var isWinnerOrAlly = HolyStoneTribeMatch.Matches(requesterTribe, zone38WinningTribe, allyOfWinner);
            return isWinnerOrAlly
                ? TribeGuardCorridorMoveOutcome.Allowed
                : HardOrSoft(originZoneId, destinationZoneId);
        }

        if (WrapCheckSpecialDestinationCatalog.TryGetRequiredRebirthCount(destinationZoneId, out var requiredRebirth))
            return moverRebirthCount == requiredRebirth
                ? TribeGuardCorridorMoveOutcome.Allowed
                : HardOrSoft(originZoneId, destinationZoneId);

        if (WrapCheckSpecialDestinationCatalog.IsInstancedDestination(destinationZoneId))
            return moverRebirthCount >= 1
                ? TribeGuardCorridorMoveOutcome.Allowed
                : HardOrSoft(originZoneId, destinationZoneId);

        // Not one of the three special groups -- the tribe-corridor group (or the unconditional default-allow
        // for everything else) is TribeGuardCorridorGate's own concern.
        return TribeGuardCorridorGate.Evaluate(corridorCatalog, corridorState, requesterTribe, originZoneId,
            destinationZoneId, requesterIsGmOrAdminRank, resolveAllyOfOwnerTribe);
    }

    private static TribeGuardCorridorMoveOutcome HardOrSoft(short originZoneId, short destinationZoneId)
    {
        return originZoneId == TribeGuardCorridorGate.HardDisconnectZoneId ||
               destinationZoneId == TribeGuardCorridorGate.HardDisconnectZoneId
            ? TribeGuardCorridorMoveOutcome.RejectedHardDisconnect
            : TribeGuardCorridorMoveOutcome.RejectedSoft;
    }
}
