using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <see cref="PortalProximityGate" /> and <see cref="PortalProximityCatalog" /> in isolation --
///     pure decision logic, no session/repository dependencies. This is a Fenrir-only hardening mechanism (see
///     both types' own remarks): there is no legacy call site to mirror, only a recovered, never-called routine
///     whose distance formula and 30-unit radius are reproduced faithfully.
/// </summary>
public class PortalProximityGateTests
{
    private const short SourceZone = 10;
    private const short TargetZone = 20;

    private static PortalProximityCatalog CatalogWithOnePortal(float x, float y, float z, short destination = TargetZone)
    {
        var portals = ImmutableDictionary<short, ImmutableArray<PortalRegistration>>.Empty
            .Add(SourceZone, [new PortalRegistration(x, y, z, destination)]);
        return new PortalProximityCatalog(portals);
    }

    [Fact]
    public void EmptyCatalog_PortalMove_IsAllowed_DocumentedNoOpUntilDataLands()
    {
        var outcome = PortalProximityGate.Evaluate(PortalProximityCatalog.Empty, SourceZone,
            requesterX: 0, requesterY: 0, requesterZ: 0,
            moveReasonSort: PortalProximityGate.PortalMoveReasonSort, targetZoneNumber: TargetZone);

        Assert.Equal(PortalProximityOutcome.Allowed, outcome);
    }

    [Fact]
    public void NonPortalMoveReason_IsAlwaysAllowed_EvenFarFromEveryRegisteredPortal()
    {
        var catalog = CatalogWithOnePortal(0, 0, 0);

        var outcome = PortalProximityGate.Evaluate(catalog, SourceZone,
            requesterX: 10_000, requesterY: 10_000, requesterZ: 10_000,
            moveReasonSort: 7 /* "return", not portal */, targetZoneNumber: TargetZone);

        Assert.Equal(PortalProximityOutcome.Allowed, outcome);
    }

    [Fact]
    public void PortalMove_WithinRadiusOfMatchingPortal_IsAllowed()
    {
        var catalog = CatalogWithOnePortal(100, 100, 0);

        // Distance = sqrt(20^2 + 0 + 0) = 20 < 30.
        var outcome = PortalProximityGate.Evaluate(catalog, SourceZone,
            requesterX: 80, requesterY: 100, requesterZ: 0,
            moveReasonSort: PortalProximityGate.PortalMoveReasonSort, targetZoneNumber: TargetZone);

        Assert.Equal(PortalProximityOutcome.Allowed, outcome);
    }

    [Fact]
    public void PortalMove_OutsideRadiusOfMatchingPortal_IsRejected()
    {
        var catalog = CatalogWithOnePortal(100, 100, 0);

        // Distance = sqrt(40^2 + 0 + 0) = 40 > 30.
        var outcome = PortalProximityGate.Evaluate(catalog, SourceZone,
            requesterX: 60, requesterY: 100, requesterZ: 0,
            moveReasonSort: PortalProximityGate.PortalMoveReasonSort, targetZoneNumber: TargetZone);

        Assert.Equal(PortalProximityOutcome.RejectedNotNearRegisteredPortal, outcome);
    }

    [Fact]
    public void PortalMove_VerticalDistanceCounts_FullSphericalRadius_NotHorizontalPlaneOnly()
    {
        var catalog = CatalogWithOnePortal(0, 0, 0);

        // Requester is horizontally exactly on the portal, but 40 units above it vertically -- outside the
        // 30-unit spherical radius (S07_MyGame03.cpp:5040-5043: true 3D distance, vertical axis included).
        var outcome = PortalProximityGate.Evaluate(catalog, SourceZone,
            requesterX: 0, requesterY: 0, requesterZ: 40,
            moveReasonSort: PortalProximityGate.PortalMoveReasonSort, targetZoneNumber: TargetZone);

        Assert.Equal(PortalProximityOutcome.RejectedNotNearRegisteredPortal, outcome);
    }

    [Fact]
    public void PortalMove_NearPortalLeadingElsewhere_IsRejected_DestinationMustMatch()
    {
        var catalog = CatalogWithOnePortal(0, 0, 0, destination: (short)99);

        var outcome = PortalProximityGate.Evaluate(catalog, SourceZone,
            requesterX: 0, requesterY: 0, requesterZ: 0,
            moveReasonSort: PortalProximityGate.PortalMoveReasonSort, targetZoneNumber: TargetZone);

        Assert.Equal(PortalProximityOutcome.RejectedNotNearRegisteredPortal, outcome);
    }

    [Fact]
    public void PortalMove_ZoneWithNoRegisteredPortalsAtAll_IsAllowed_DocumentedNoOp()
    {
        var catalog = CatalogWithOnePortal(0, 0, 0);

        var outcome = PortalProximityGate.Evaluate(catalog, sourceZoneId: 999,
            requesterX: 0, requesterY: 0, requesterZ: 0,
            moveReasonSort: PortalProximityGate.PortalMoveReasonSort, targetZoneNumber: TargetZone);

        Assert.Equal(PortalProximityOutcome.Allowed, outcome);
    }

    [Fact]
    public void TryGetPortals_EmptyCatalog_NeverHasAnyZone()
    {
        Assert.False(PortalProximityCatalog.Empty.TryGetPortals(SourceZone, out _));
    }
}
