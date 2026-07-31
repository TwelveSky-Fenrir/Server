using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World.Configuration;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Domain.World;

public enum PortalTransitionRouteKind
{
    SymbolBattleLockout,

    DistantZoneToTown,

    Zone038WinnerReward,

    Zone038TicketGate,

    HubInstance,

    Unmapped
}

public static class PortalTransitionRoutes
{
    public const short Zone038Id = 38;

    public const short Zone038TicketDestinationId = 310;

    public const short HubZoneId = 74;

    public const short InstancedZoneId = 303;

    private static readonly FrozenDictionary<short, short> TownByDistantZone = new Dictionary<short, short>
    {
        [251] = 1,
        [252] = 1,
        [259] = 1,
        [260] = 1,
        [253] = 6,
        [254] = 6,
        [261] = 6,
        [262] = 6,
        [255] = 11,
        [256] = 11,
        [263] = 11,
        [264] = 11,
        [257] = 140,
        [258] = 140,
        [265] = 140,
        [266] = 140
    }.ToFrozenDictionary();

    private static readonly FrozenSet<short> Zone038WinnerDestinations =
        new short[] { 39, 144, 145, 313, 314, 315, 316 }.ToFrozenSet();

    public static PortalTransitionRouteKind Classify(short originZoneNumber, short destinationZoneNumber,
        bool tribeSymbolBattleActive)
    {
        if (TribeSymbolBattleZoneLockout.IsLockedOut(originZoneNumber, destinationZoneNumber,
                tribeSymbolBattleActive))
            return PortalTransitionRouteKind.SymbolBattleLockout;

        if (TownByDistantZone.TryGetValue(originZoneNumber, out var pairedTown) &&
            pairedTown == destinationZoneNumber)
            return PortalTransitionRouteKind.DistantZoneToTown;

        if (originZoneNumber == Zone038Id)
        {
            if (destinationZoneNumber == Zone038TicketDestinationId)
                return PortalTransitionRouteKind.Zone038TicketGate;

            if (Zone038WinnerDestinations.Contains(destinationZoneNumber))
                return PortalTransitionRouteKind.Zone038WinnerReward;
        }

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

    RejectedRouteIneligible,

    RejectedRouteUnmapped
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
        byte? zone38WinningTribe,
        int avatarZone038TicketCount)
    {
        if (!ZoneConfigCatalog.IsValidZoneNumber(originZoneNumber) ||
            !ZoneConfigCatalog.IsValidZoneNumber(destinationZoneNumber))
            return PortalTransitionEligibilityOutcome.RejectedZoneNumberOutOfRange;

        var routeKind = PortalTransitionRoutes.Classify(originZoneNumber, destinationZoneNumber,
            tribeSymbolBattleActive);

        if (routeKind == PortalTransitionRouteKind.Unmapped)
            return PortalTransitionEligibilityOutcome.RejectedRouteUnmapped;

        var isEligible = routeKind switch
        {
            PortalTransitionRouteKind.SymbolBattleLockout => false,
            PortalTransitionRouteKind.DistantZoneToTown =>
                IsWithinTraversalLevelBand(zoneConfig, originZoneNumber, destinationZoneNumber, avatarCombinedLevel),
            PortalTransitionRouteKind.Zone038TicketGate =>
                (avatarZone038TicketCount > 0 ||
                 (zone38WinningTribe is { } ticketWinner && avatarTribe == ticketWinner)) &&
                zoneConfig.IsWithinLevelBand(destinationZoneNumber, avatarCombinedLevel),
            PortalTransitionRouteKind.Zone038WinnerReward =>
                zone38WinningTribe is { } rewardWinner &&
                (avatarTribe == rewardWinner || avatarAlliedTribe == rewardWinner) &&
                zoneConfig.IsWithinLevelBand(destinationZoneNumber, avatarCombinedLevel),
            PortalTransitionRouteKind.HubInstance =>
                zoneConfig.IsWithinLevelBand(destinationZoneNumber, avatarCombinedLevel) &&
                avatarRebirthCount == RebirthProgression.MaxRebirthGeneration &&
                zone38WinningTribe is { } winningTribe &&
                (avatarTribe == winningTribe || avatarAlliedTribe == winningTribe),
            PortalTransitionRouteKind.Unmapped => false,
            _ => false
        };

        return isEligible
            ? PortalTransitionEligibilityOutcome.Eligible
            : PortalTransitionEligibilityOutcome.RejectedRouteIneligible;
    }

    private static bool IsWithinTraversalLevelBand(ZoneConfigCatalog zoneConfig, short originZoneNumber,
        short destinationZoneNumber, int avatarCombinedLevel)
    {
        var originMaxLevel = zoneConfig.GetMaxLevel(originZoneNumber);
        return avatarCombinedLevel >= zoneConfig.GetMinLevel(destinationZoneNumber) &&
               (originMaxLevel == 0 || avatarCombinedLevel <= originMaxLevel);
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
        int avatarZone038TicketCount,
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
                    tribeSymbolBattleActive, zone38WinningTribe, avatarZone038TicketCount) !=
                PortalTransitionEligibilityOutcome.Eligible)
                continue;

            destinationZoneNumber = portal.DestinationZoneId;
            return true;
        }

        return false;
    }
}
