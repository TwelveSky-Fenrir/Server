namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class WrapCheckSpecialDestinationGate
{
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
