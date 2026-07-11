using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.World;

public class PortalProximityGateTests
{
    private const short SourceZone = 10;
    private const short TargetZone = 20;

    private static PortalProximityCatalog CatalogWithOnePortal(float x, float y, float z,
        short destination = TargetZone)
    {
        var portals = ImmutableDictionary<short, ImmutableArray<PortalRegistration>>.Empty
            .Add(SourceZone, [new PortalRegistration(x, y, z, destination)]);
        return new PortalProximityCatalog(portals);
    }

    [Fact]
    public void EmptyCatalog_PortalMove_IsAllowed_DocumentedNoOpUntilDataLands()
    {
        var outcome = PortalProximityGate.Evaluate(PortalProximityCatalog.Empty, SourceZone,
            0, 0, 0,
            PortalProximityGate.PortalMoveReasonSort, TargetZone);

        Assert.Equal(PortalProximityOutcome.Allowed, outcome);
    }

    [Fact]
    public void NonPortalMoveReason_IsAlwaysAllowed_EvenFarFromEveryRegisteredPortal()
    {
        var catalog = CatalogWithOnePortal(0, 0, 0);

        var outcome = PortalProximityGate.Evaluate(catalog, SourceZone,
            10_000, 10_000, 10_000,
            7, TargetZone);

        Assert.Equal(PortalProximityOutcome.Allowed, outcome);
    }

    [Fact]
    public void PortalMove_WithinRadiusOfMatchingPortal_IsAllowed()
    {
        var catalog = CatalogWithOnePortal(100, 100, 0);

        var outcome = PortalProximityGate.Evaluate(catalog, SourceZone,
            80, 100, 0,
            PortalProximityGate.PortalMoveReasonSort, TargetZone);

        Assert.Equal(PortalProximityOutcome.Allowed, outcome);
    }

    [Fact]
    public void PortalMove_OutsideRadiusOfMatchingPortal_IsRejected()
    {
        var catalog = CatalogWithOnePortal(100, 100, 0);

        var outcome = PortalProximityGate.Evaluate(catalog, SourceZone,
            60, 100, 0,
            PortalProximityGate.PortalMoveReasonSort, TargetZone);

        Assert.Equal(PortalProximityOutcome.RejectedNotNearRegisteredPortal, outcome);
    }

    [Fact]
    public void PortalMove_VerticalDistanceCounts_FullSphericalRadius_NotHorizontalPlaneOnly()
    {
        var catalog = CatalogWithOnePortal(0, 0, 0);

        var outcome = PortalProximityGate.Evaluate(catalog, SourceZone,
            0, 0, 40,
            PortalProximityGate.PortalMoveReasonSort, TargetZone);

        Assert.Equal(PortalProximityOutcome.RejectedNotNearRegisteredPortal, outcome);
    }

    [Fact]
    public void PortalMove_NearPortalLeadingElsewhere_IsRejected_DestinationMustMatch()
    {
        var catalog = CatalogWithOnePortal(0, 0, 0, 99);

        var outcome = PortalProximityGate.Evaluate(catalog, SourceZone,
            0, 0, 0,
            PortalProximityGate.PortalMoveReasonSort, TargetZone);

        Assert.Equal(PortalProximityOutcome.RejectedNotNearRegisteredPortal, outcome);
    }

    [Fact]
    public void PortalMove_ZoneWithNoRegisteredPortalsAtAll_IsAllowed_DocumentedNoOp()
    {
        var catalog = CatalogWithOnePortal(0, 0, 0);

        var outcome = PortalProximityGate.Evaluate(catalog, 999,
            0, 0, 0,
            PortalProximityGate.PortalMoveReasonSort, TargetZone);

        Assert.Equal(PortalProximityOutcome.Allowed, outcome);
    }

    [Fact]
    public void TryGetPortals_EmptyCatalog_NeverHasAnyZone()
    {
        Assert.False(PortalProximityCatalog.Empty.TryGetPortals(SourceZone, out _));
    }
}
