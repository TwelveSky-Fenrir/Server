using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Configuration;

namespace Fenrir.Application.Game.Tests.World;

public class PortalTransitionEligibilityTests
{
    private static ZoneConfigCatalog BuildZoneConfig(params (int MapId, ZoneConfig Config)[] entries)
    {
        return new ZoneConfigCatalog(entries.Select(e => new KeyValuePair<int, ZoneConfig>(e.MapId, e.Config)));
    }

    [Theory]
    [InlineData(40, 38, true)]
    [InlineData(41, 38, true)]
    [InlineData(42, 38, true)]
    [InlineData(39, 38, false)]
    [InlineData(40, 37, false)]
    public void Classify_SymbolBattleLockoutRoutes(short origin, short destination, bool expectedLockout)
    {
        var kind = PortalTransitionRoutes.Classify(origin, destination, tribeSymbolBattleActive: true);

        Assert.Equal(expectedLockout, kind == PortalTransitionRouteKind.SymbolBattleLockout);
    }

    [Fact]
    public void Classify_SymbolBattleRoute_WithFlagInactive_IsNotLockout()
    {
        var kind = PortalTransitionRoutes.Classify(40, 38, tribeSymbolBattleActive: false);

        Assert.NotEqual(PortalTransitionRouteKind.SymbolBattleLockout, kind);
    }

    [Theory]
    [InlineData(1, 251, true)]
    [InlineData(6, 266, true)]
    [InlineData(11, 260, true)]
    [InlineData(140, 251, true)]
    [InlineData(1, 250, false)]
    [InlineData(1, 267, false)]
    [InlineData(2, 260, false)]
    public void Classify_TownToDistantZoneRoutes(short origin, short destination, bool expectedTownRoute)
    {
        var kind = PortalTransitionRoutes.Classify(origin, destination, tribeSymbolBattleActive: false);

        Assert.Equal(expectedTownRoute, kind == PortalTransitionRouteKind.TownToDistantZone);
    }

    [Theory]
    [InlineData(74, 303)]
    [InlineData(303, 74)]
    public void Classify_HubInstanceRoute_IsSymmetric(short origin, short destination)
    {
        var kind = PortalTransitionRoutes.Classify(origin, destination, tribeSymbolBattleActive: false);

        Assert.Equal(PortalTransitionRouteKind.HubInstance, kind);
    }

    [Fact]
    public void Classify_UnrelatedRoute_IsUnmapped()
    {
        var kind = PortalTransitionRoutes.Classify(50, 60, tribeSymbolBattleActive: false);

        Assert.Equal(PortalTransitionRouteKind.Unmapped, kind);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 351)]
    [InlineData(-1, 100)]
    public void Resolve_ZoneNumberOutOfRange_IsRejectedBeforeAnyRouteInspection(short origin, short destination)
    {
        var outcome = PortalTransitionEligibilityRules.Resolve(ZoneConfigCatalog.Empty, origin, destination,
            avatarCombinedLevel: 100, avatarRebirthCount: 12, avatarTribe: 0, avatarAlliedTribe: null,
            tribeSymbolBattleActive: false, zone38WinningTribe: null);

        Assert.Equal(PortalTransitionEligibilityOutcome.RejectedZoneNumberOutOfRange, outcome);
    }

    [Fact]
    public void Resolve_SymbolBattleLockout_IsIneligibleWhileFlagActive()
    {
        var outcome = PortalTransitionEligibilityRules.Resolve(ZoneConfigCatalog.Empty, 40, 38,
            avatarCombinedLevel: 100, avatarRebirthCount: 0, avatarTribe: 0, avatarAlliedTribe: null,
            tribeSymbolBattleActive: true, zone38WinningTribe: null);

        Assert.Equal(PortalTransitionEligibilityOutcome.RejectedRouteIneligible, outcome);
    }

    [Fact]
    public void Resolve_UnmappedRoute_IsAlwaysIneligible()
    {
        var outcome = PortalTransitionEligibilityRules.Resolve(ZoneConfigCatalog.Empty, 50, 60,
            avatarCombinedLevel: 100, avatarRebirthCount: 12, avatarTribe: 1, avatarAlliedTribe: 2,
            tribeSymbolBattleActive: false, zone38WinningTribe: 1);

        Assert.Equal(PortalTransitionEligibilityOutcome.RejectedRouteIneligible, outcome);
    }

    [Fact]
    public void Resolve_TownRoute_MatchingTribeAndWithinLevelBand_IsEligible()
    {
        var zoneConfig = BuildZoneConfig(
            (1, new ZoneConfig { OwnerTribe = 0 }),
            (260, new ZoneConfig { MinLevel = 100, MaxLevel = 157 }));

        var outcome = PortalTransitionEligibilityRules.Resolve(zoneConfig, 1, 260,
            avatarCombinedLevel: 120, avatarRebirthCount: 0, avatarTribe: 0, avatarAlliedTribe: null,
            tribeSymbolBattleActive: false, zone38WinningTribe: null);

        Assert.Equal(PortalTransitionEligibilityOutcome.Eligible, outcome);
    }

    [Fact]
    public void Resolve_TownRoute_TribeMismatch_IsRejectedRegardlessOfLevel()
    {
        var zoneConfig = BuildZoneConfig(
            (1, new ZoneConfig { OwnerTribe = 0 }),
            (260, new ZoneConfig { MinLevel = 0, MaxLevel = 157 }));

        var outcome = PortalTransitionEligibilityRules.Resolve(zoneConfig, 1, 260,
            avatarCombinedLevel: 120, avatarRebirthCount: 0, avatarTribe: 1, avatarAlliedTribe: null,
            tribeSymbolBattleActive: false, zone38WinningTribe: null);

        Assert.Equal(PortalTransitionEligibilityOutcome.RejectedRouteIneligible, outcome);
    }

    [Fact]
    public void Resolve_TownRoute_TribeMatchesButLevelOutOfBand_IsRejected()
    {
        var zoneConfig = BuildZoneConfig(
            (1, new ZoneConfig { OwnerTribe = 0 }),
            (260, new ZoneConfig { MinLevel = 100, MaxLevel = 157 }));

        var outcome = PortalTransitionEligibilityRules.Resolve(zoneConfig, 1, 260,
            avatarCombinedLevel: 50, avatarRebirthCount: 0, avatarTribe: 0, avatarAlliedTribe: null,
            tribeSymbolBattleActive: false, zone38WinningTribe: null);

        Assert.Equal(PortalTransitionEligibilityOutcome.RejectedRouteIneligible, outcome);
    }

    [Theory]
    [InlineData(74, 303)]
    [InlineData(303, 74)]
    public void Resolve_HubInstanceRoute_AllThreeConditionsMet_IsEligible(short origin, short destination)
    {
        var zoneConfig = BuildZoneConfig(
            (74, new ZoneConfig { MinLevel = 100, MaxLevel = 157 }),
            (303, new ZoneConfig { MinLevel = 100, MaxLevel = 157 }));

        var outcome = PortalTransitionEligibilityRules.Resolve(zoneConfig, origin, destination,
            avatarCombinedLevel: 150, avatarRebirthCount: 12, avatarTribe: 2, avatarAlliedTribe: null,
            tribeSymbolBattleActive: false, zone38WinningTribe: 2);

        Assert.Equal(PortalTransitionEligibilityOutcome.Eligible, outcome);
    }

    [Fact]
    public void Resolve_HubInstanceRoute_ViaAlliedTribeInsteadOfOwnTribe_IsEligible()
    {
        var zoneConfig = BuildZoneConfig((303, new ZoneConfig { MinLevel = 0, MaxLevel = 157 }));

        var outcome = PortalTransitionEligibilityRules.Resolve(zoneConfig, 74, 303,
            avatarCombinedLevel: 150, avatarRebirthCount: 12, avatarTribe: 1, avatarAlliedTribe: 3,
            tribeSymbolBattleActive: false, zone38WinningTribe: 3);

        Assert.Equal(PortalTransitionEligibilityOutcome.Eligible, outcome);
    }

    [Fact]
    public void Resolve_HubInstanceRoute_RebirthBelowMaximum_IsRejected_NotPartialCredit()
    {
        var zoneConfig = BuildZoneConfig((303, new ZoneConfig { MinLevel = 0, MaxLevel = 157 }));

        var outcome = PortalTransitionEligibilityRules.Resolve(zoneConfig, 74, 303,
            avatarCombinedLevel: 150, avatarRebirthCount: 11, avatarTribe: 2, avatarAlliedTribe: null,
            tribeSymbolBattleActive: false, zone38WinningTribe: 2);

        Assert.Equal(PortalTransitionEligibilityOutcome.RejectedRouteIneligible, outcome);
    }

    [Fact]
    public void Resolve_HubInstanceRoute_TribeDoesNotMatchWinningTribe_IsRejected()
    {
        var zoneConfig = BuildZoneConfig((303, new ZoneConfig { MinLevel = 0, MaxLevel = 157 }));

        var outcome = PortalTransitionEligibilityRules.Resolve(zoneConfig, 74, 303,
            avatarCombinedLevel: 150, avatarRebirthCount: 12, avatarTribe: 2, avatarAlliedTribe: null,
            tribeSymbolBattleActive: false, zone38WinningTribe: 3);

        Assert.Equal(PortalTransitionEligibilityOutcome.RejectedRouteIneligible, outcome);
    }

    [Fact]
    public void Resolve_HubInstanceRoute_NoWinningTribeEstablishedYet_IsRejected()
    {
        var zoneConfig = BuildZoneConfig((303, new ZoneConfig { MinLevel = 0, MaxLevel = 157 }));

        var outcome = PortalTransitionEligibilityRules.Resolve(zoneConfig, 74, 303,
            avatarCombinedLevel: 150, avatarRebirthCount: 12, avatarTribe: 2, avatarAlliedTribe: null,
            tribeSymbolBattleActive: false, zone38WinningTribe: null);

        Assert.Equal(PortalTransitionEligibilityOutcome.RejectedRouteIneligible, outcome);
    }

    private static PortalProximityCatalog CatalogWithPortals(short sourceZone,
        params (float X, float Y, float Z, short DestinationZoneId)[] portals)
    {
        var registrations = portals.Select(p => new PortalRegistration(p.X, p.Y, p.Z, p.DestinationZoneId))
            .ToImmutableArray();
        var byZone = ImmutableDictionary<short, ImmutableArray<PortalRegistration>>.Empty.Add(sourceZone,
            registrations);
        return new PortalProximityCatalog(byZone);
    }

    [Fact]
    public void TryResolveDestination_NoRegisteredPortalsForCurrentZone_ReturnsFalse()
    {
        var resolved = PortalTransitionDestinationResolver.TryResolveDestination(PortalProximityCatalog.Empty,
            ZoneConfigCatalog.Empty, 10, 0, 0, 0, avatarCombinedLevel: 1, avatarRebirthCount: 0, avatarTribe: 0,
            avatarAlliedTribe: null, tribeSymbolBattleActive: false, zone38WinningTribe: null,
            out var destination);

        Assert.False(resolved);
        Assert.Equal(0, destination);
    }

    [Fact]
    public void TryResolveDestination_CurrentZoneOutOfRange_ReturnsFalse()
    {
        var catalog = CatalogWithPortals(999, (0, 0, 0, 20));

        var resolved = PortalTransitionDestinationResolver.TryResolveDestination(catalog, ZoneConfigCatalog.Empty,
            999, 0, 0, 0, avatarCombinedLevel: 1, avatarRebirthCount: 0, avatarTribe: 0, avatarAlliedTribe: null,
            tribeSymbolBattleActive: false, zone38WinningTribe: null, out var destination);

        Assert.False(resolved);
        Assert.Equal(0, destination);
    }

    [Fact]
    public void TryResolveDestination_ExactlyAtThirtyUnits_IsNotWithinRadius_EvenForAnOtherwiseEligibleRoute()
    {
        var zoneConfig = BuildZoneConfig((303, new ZoneConfig { MinLevel = 0, MaxLevel = 157 }));
        var catalog = CatalogWithPortals(74, (30, 0, 0, 303));

        var resolved = PortalTransitionDestinationResolver.TryResolveDestination(catalog, zoneConfig,
            74, 0, 0, 0, avatarCombinedLevel: 150, avatarRebirthCount: 12, avatarTribe: 2, avatarAlliedTribe: null,
            tribeSymbolBattleActive: false, zone38WinningTribe: 2, out _);

        Assert.False(resolved);
    }

    [Fact]
    public void TryResolveDestination_FirstRegisteredEligibleCandidate_WinsOverLaterCloserEligibleCandidate()
    {
        var zoneConfig = BuildZoneConfig(
            (1, new ZoneConfig { OwnerTribe = 0 }),
            (255, new ZoneConfig { MinLevel = 0, MaxLevel = 157 }),
            (260, new ZoneConfig { MinLevel = 0, MaxLevel = 157 }));
        var catalog = CatalogWithPortals(1,
            (20, 0, 0, 255),
            (5, 0, 0, 260));

        var resolved = PortalTransitionDestinationResolver.TryResolveDestination(catalog, zoneConfig,
            1, 0, 0, 0, avatarCombinedLevel: 100, avatarRebirthCount: 0, avatarTribe: 0, avatarAlliedTribe: null,
            tribeSymbolBattleActive: false, zone38WinningTribe: null, out var destination);

        Assert.True(resolved);
        Assert.Equal((short)255, destination);
    }

    [Fact]
    public void TryResolveDestination_FirstCandidateWithinRangeButIneligible_FallsThroughToNextEligibleCandidate()
    {
        var zoneConfig = BuildZoneConfig((303, new ZoneConfig { MinLevel = 0, MaxLevel = 157 }));
        var catalog = CatalogWithPortals(74,
            (0, 0, 0, 100),
            (0, 0, 0, 303));

        var resolved = PortalTransitionDestinationResolver.TryResolveDestination(catalog, zoneConfig,
            74, 0, 0, 0, avatarCombinedLevel: 150, avatarRebirthCount: 12, avatarTribe: 2, avatarAlliedTribe: null,
            tribeSymbolBattleActive: false, zone38WinningTribe: 2, out var destination);

        Assert.True(resolved);
        Assert.Equal((short)303, destination);
    }

    [Fact]
    public void TryResolveDestination_NoCandidateEligible_ReturnsFalse()
    {
        var catalog = CatalogWithPortals(40, (0, 0, 0, 38));

        var resolved = PortalTransitionDestinationResolver.TryResolveDestination(catalog, ZoneConfigCatalog.Empty,
            40, 0, 0, 0, avatarCombinedLevel: 1, avatarRebirthCount: 0, avatarTribe: 0, avatarAlliedTribe: null,
            tribeSymbolBattleActive: true, zone38WinningTribe: null, out var destination);

        Assert.False(resolved);
        Assert.Equal(0, destination);
    }
}
