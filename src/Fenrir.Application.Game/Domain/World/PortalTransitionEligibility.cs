using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World.Configuration;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Domain.World;

public enum PortalTransitionRouteKind
{
    SymbolBattleLockout,

    TownToDistantZone,

    HubInstance,

    Unmapped
}

public static class PortalTransitionRoutes
{
    public const short DistantZoneRangeStart = 251;

    public const short DistantZoneRangeEnd = 266;

    public const short HubZoneId = 74;

    public const short InstancedZoneId = 303;
    public static readonly IReadOnlySet<short> TownOriginZoneIds = new HashSet<short> { 1, 6, 11, 140 };

    public static PortalTransitionRouteKind Classify(short originZoneNumber, short destinationZoneNumber,
        bool tribeSymbolBattleActive)
    {
        if (TribeSymbolBattleZoneLockout.IsLockedOut(originZoneNumber, destinationZoneNumber,
                tribeSymbolBattleActive))
            return PortalTransitionRouteKind.SymbolBattleLockout;

        if (TownOriginZoneIds.Contains(originZoneNumber) &&
            destinationZoneNumber is >= DistantZoneRangeStart and <= DistantZoneRangeEnd)
            return PortalTransitionRouteKind.TownToDistantZone;

        if ((originZoneNumber == HubZoneId && destinationZoneNumber == InstancedZoneId) ||
            (originZoneNumber == InstancedZoneId && destinationZoneNumber == HubZoneId))
            return PortalTransitionRouteKind.HubInstance;

        return PortalTransitionRouteKind.Unmapped;
    }
}

public enum PortalTransitionEligibilityOutcome
{
    Eligible,

    RejectedZoneNumberOutOfRange,

    RejectedRouteIneligible
}

public static class PortalTransitionEligibilityRules
{
    public static PortalTransitionEligibilityOutcome Resolve(
        ZoneConfigCatalog zoneConfig,
        short originZoneNumber,
        short destinationZoneNumber,
        int avatarCombinedLevel,
        int avatarRebirthCount,
        byte avatarTribe,
        byte? avatarAlliedTribe,
        bool tribeSymbolBattleActive,
        byte? zone38WinningTribe)
    {
        if (!ZoneConfigCatalog.IsValidZoneNumber(originZoneNumber) ||
            !ZoneConfigCatalog.IsValidZoneNumber(destinationZoneNumber))
            return PortalTransitionEligibilityOutcome.RejectedZoneNumberOutOfRange;

        var routeKind = PortalTransitionRoutes.Classify(originZoneNumber, destinationZoneNumber,
            tribeSymbolBattleActive);

        var isEligible = routeKind switch
        {
            PortalTransitionRouteKind.SymbolBattleLockout => false,
            PortalTransitionRouteKind.TownToDistantZone =>
                zoneConfig.GetOwnerTribe(originZoneNumber) == avatarTribe &&
                zoneConfig.IsWithinLevelBand(destinationZoneNumber, avatarCombinedLevel),
            PortalTransitionRouteKind.HubInstance =>
                zoneConfig.IsWithinLevelBand(destinationZoneNumber, avatarCombinedLevel) &&
                avatarRebirthCount == RebirthProgression.MaxRebirthGeneration &&
                zone38WinningTribe is { } winningTribe &&
                (avatarTribe == winningTribe || avatarAlliedTribe == winningTribe),
            _ => false
        };

        return isEligible
            ? PortalTransitionEligibilityOutcome.Eligible
            : PortalTransitionEligibilityOutcome.RejectedRouteIneligible;
    }
}

public static class PortalTransitionDestinationResolver
{
    public static bool TryResolveDestination(
        PortalProximityCatalog portalCatalog,
        ZoneConfigCatalog zoneConfig,
        short currentZoneNumber,
        float positionX,
        float positionY,
        float positionZ,
        int avatarCombinedLevel,
        int avatarRebirthCount,
        byte avatarTribe,
        byte? avatarAlliedTribe,
        bool tribeSymbolBattleActive,
        byte? zone38WinningTribe,
        out short destinationZoneNumber)
    {
        destinationZoneNumber = default;

        if (!ZoneConfigCatalog.IsValidZoneNumber(currentZoneNumber) ||
            !portalCatalog.TryGetPortals(currentZoneNumber, out var portals))
            return false;

        const float radiusSquared = PortalProximityGate.ProximityRadius * PortalProximityGate.ProximityRadius;

        foreach (var portal in portals)
        {
            var dx = portal.X - positionX;
            var dy = portal.Y - positionY;
            var dz = portal.Z - positionZ;

            if (dx * dx + dy * dy + dz * dz >= radiusSquared)
                continue;

            if (PortalTransitionEligibilityRules.Resolve(zoneConfig, currentZoneNumber, portal.DestinationZoneId,
                    avatarCombinedLevel, avatarRebirthCount, avatarTribe, avatarAlliedTribe,
                    tribeSymbolBattleActive, zone38WinningTribe) != PortalTransitionEligibilityOutcome.Eligible)
                continue;

            destinationZoneNumber = portal.DestinationZoneId;
            return true;
        }

        return false;
    }
}
