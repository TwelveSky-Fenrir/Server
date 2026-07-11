namespace Fenrir.Application.Game.Domain.World;

public enum PortalProximityOutcome
{
    Allowed,

    RejectedNotNearRegisteredPortal
}

public static class PortalProximityGate
{
    public const int PortalMoveReasonSort = 4;

    public const float ProximityRadius = 30f;

    private const float ProximityRadiusSquared = ProximityRadius * ProximityRadius;

    public static PortalProximityOutcome Evaluate(
        PortalProximityCatalog catalog,
        short sourceZoneId,
        float requesterX,
        float requesterY,
        float requesterZ,
        int moveReasonSort,
        short targetZoneNumber)
    {
        if (moveReasonSort != PortalMoveReasonSort)
            return PortalProximityOutcome.Allowed;

        if (!catalog.TryGetPortals(sourceZoneId, out var portals))
            return PortalProximityOutcome.Allowed;

        foreach (var portal in portals)
        {
            if (portal.DestinationZoneId != targetZoneNumber)
                continue;

            var dx = portal.X - requesterX;
            var dy = portal.Y - requesterY;
            var dz = portal.Z - requesterZ;
            if (dx * dx + dy * dy + dz * dz < ProximityRadiusSquared)
                return PortalProximityOutcome.Allowed;
        }

        return PortalProximityOutcome.RejectedNotNearRegisteredPortal;
    }
}
