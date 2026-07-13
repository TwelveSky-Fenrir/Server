namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum TribeGuardCorridorMoveOutcome
{
    Allowed,
    RejectedSoft,
    RejectedHardDisconnect
}

public static class TribeGuardCorridorGate
{
    public const short HardDisconnectZoneId = 37;

    public static TribeGuardCorridorMoveOutcome Evaluate(
        TribeGuardCorridorCatalog catalog,
        TribeGuardCorridorState state,
        byte requesterTribe,
        short originZoneId,
        short destinationZoneId,
        bool requesterIsGmOrAdminRank,
        Func<byte, byte?>? resolveAllyOfOwnerTribe = null)
    {
        if (requesterIsGmOrAdminRank)
            return TribeGuardCorridorMoveOutcome.Allowed;

        if (!catalog.TryGetSegmentForDestinationZone(destinationZoneId, out var ownerTribeId, out var segmentIndex))
            return TribeGuardCorridorMoveOutcome.Allowed;

        if (requesterTribe == ownerTribeId)
            return TribeGuardCorridorMoveOutcome.Allowed;

        if (resolveAllyOfOwnerTribe?.Invoke(ownerTribeId) is { } allyOfOwner && requesterTribe == allyOfOwner)
            return TribeGuardCorridorMoveOutcome.Allowed;

        var originSegmentIndex = catalog.GetOriginSegmentIndex(ownerTribeId, originZoneId);

        if (originSegmentIndex is { } deeperOrigin && deeperOrigin > segmentIndex)
            return TribeGuardCorridorMoveOutcome.Allowed;

        var isValidSingleStepAdvance = originSegmentIndex == segmentIndex - 1;
        if (!isValidSingleStepAdvance)
            return HardOrSoft(originZoneId, destinationZoneId);

        return state.IsOpen(ownerTribeId, segmentIndex)
            ? TribeGuardCorridorMoveOutcome.Allowed
            : HardOrSoft(originZoneId, destinationZoneId);
    }

    private static TribeGuardCorridorMoveOutcome HardOrSoft(short originZoneId, short destinationZoneId)
    {
        return originZoneId == HardDisconnectZoneId || destinationZoneId == HardDisconnectZoneId
            ? TribeGuardCorridorMoveOutcome.RejectedHardDisconnect
            : TribeGuardCorridorMoveOutcome.RejectedSoft;
    }
}
