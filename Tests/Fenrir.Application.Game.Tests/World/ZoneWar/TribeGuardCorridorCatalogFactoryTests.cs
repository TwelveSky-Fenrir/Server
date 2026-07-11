using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="TribeGuardCorridorCatalogFactory" /> -- the real sixteen-corridor-zone / shared-hub
///     table (A8-corridor-respawn contract), as opposed to <see cref="TribeGuardCorridorCatalogTests" />'s own
///     synthetic two-tribe fixture.
/// </summary>
public class TribeGuardCorridorCatalogFactoryTests
{
    [Fact]
    public void BuildLive_HubZoneIs38()
    {
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();

        Assert.Equal((short)38, catalog.HubZoneId);
    }

    [Theory]
    // Tribe 0: town 1, corridor 2/3/4, innermost-first segment order [4, 3, 2, 1].
    [InlineData((short)4, (byte)0, (byte)0)]
    [InlineData((short)3, (byte)0, (byte)1)]
    [InlineData((short)2, (byte)0, (byte)2)]
    [InlineData((short)1, (byte)0, (byte)3)]
    // Tribe 1: town 6, corridor 7/8/9.
    [InlineData((short)9, (byte)1, (byte)0)]
    [InlineData((short)8, (byte)1, (byte)1)]
    [InlineData((short)7, (byte)1, (byte)2)]
    [InlineData((short)6, (byte)1, (byte)3)]
    // Tribe 2: town 11, corridor 12/13/14.
    [InlineData((short)14, (byte)2, (byte)0)]
    [InlineData((short)13, (byte)2, (byte)1)]
    [InlineData((short)12, (byte)2, (byte)2)]
    [InlineData((short)11, (byte)2, (byte)3)]
    // Tribe 3: town 140, corridor 141/142/143.
    [InlineData((short)143, (byte)3, (byte)0)]
    [InlineData((short)142, (byte)3, (byte)1)]
    [InlineData((short)141, (byte)3, (byte)2)]
    [InlineData((short)140, (byte)3, (byte)3)]
    public void BuildLive_ResolvesEveryCorridorAndTownZoneToItsOwnTribeAndSegment(short zoneId, byte expectedTribe,
        byte expectedSegment)
    {
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();

        Assert.True(catalog.TryGetSegmentForDestinationZone(zoneId, out var tribe, out var segment));
        Assert.Equal(expectedTribe, tribe);
        Assert.Equal(expectedSegment, segment);
    }

    [Fact]
    public void BuildLive_TheHubItselfIsNotACorridorDestination()
    {
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();

        Assert.False(catalog.TryGetSegmentForDestinationZone(38, out _, out _));
    }

    [Fact]
    public void BuildLive_AnUnrelatedOrdinaryHuntingZoneIsNotGated()
    {
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();

        // Contract edge case: "destination not in the table" -- zones 5, 10, 37, 38 are explicitly called out
        // as always permitted since they are never named in the corridor switch.
        Assert.False(catalog.TryGetSegmentForDestinationZone(5, out _, out _));
        Assert.False(catalog.TryGetSegmentForDestinationZone(10, out _, out _));
        Assert.False(catalog.TryGetSegmentForDestinationZone(37, out _, out _));
    }

    [Fact]
    public void BuildLive_HubOwnsEveryTribesSegmentZeroInOnePass()
    {
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();

        var owned = catalog.GetSegmentsOwnedByZone(38);

        Assert.Equal(4, owned.Count);
        Assert.Contains(((byte)0, (byte)0), owned);
        Assert.Contains(((byte)1, (byte)0), owned);
        Assert.Contains(((byte)2, (byte)0), owned);
        Assert.Contains(((byte)3, (byte)0), owned);
    }

    [Theory]
    [InlineData((short)4, (byte)0, (byte)1)] // tribe 0's innermost corridor zone owns the next (segment 1) stone
    [InlineData((short)3, (byte)0, (byte)2)]
    [InlineData((short)2, (byte)0, (byte)3)] // outermost corridor zone owns the stone gating the town
    public void BuildLive_ACorridorZoneOwnsOnlyTheNextSegmentOfItsOwnTribe(short zoneId, byte expectedTribe,
        byte expectedSegment)
    {
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();

        var owned = catalog.GetSegmentsOwnedByZone(zoneId);

        Assert.Single(owned);
        Assert.Equal((expectedTribe, expectedSegment), owned[0]);
    }

    [Theory]
    [InlineData((short)1)] // tribe 0's town
    [InlineData((short)6)] // tribe 1's town
    [InlineData((short)11)] // tribe 2's town
    [InlineData((short)140)] // tribe 3's town
    public void BuildLive_TownZonesOwnNoStoneAtAll(short townZoneId)
    {
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();

        Assert.Empty(catalog.GetSegmentsOwnedByZone(townZoneId));
    }

    [Fact]
    public void BuildLive_GuardPostSlotsAreNotYetConfigured_DocumentedGapNotAGuess()
    {
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();

        Assert.False(catalog.TryGetGuardPostSlots(0, 0, out _));
    }

    [Fact]
    public void BuildLive_ComposedWithTheGate_EnemyAdvanceIntoTownIsGatedByGuardState()
    {
        // End-to-end sanity check combining the real catalog with the real gate: an enemy of tribe 0 (tribe 1)
        // trying to step from zone 2 (segment 3's own adjacent zone) into the town (zone 1) is blocked while
        // segment 3 is closed (the default boot state) and allowed once it opens.
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();
        var state = new TribeGuardCorridorState();

        var blocked = TribeGuardCorridorGate.Evaluate(catalog, state, requesterTribe: 1, originZoneId: 2,
            destinationZoneId: 1, requesterIsGmOrAdminRank: false);
        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, blocked);

        state.TrySetOpen(0, 3, true);

        var allowed = TribeGuardCorridorGate.Evaluate(catalog, state, requesterTribe: 1, originZoneId: 2,
            destinationZoneId: 1, requesterIsGmOrAdminRank: false);
        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, allowed);
    }

    [Fact]
    public void BuildLive_ComposedWithTheGate_EnemyRetreatingTowardTheHubIsAlwaysAllowed()
    {
        // Contract: "The outward direction (town toward center) is freely permitted once inside." Moving from
        // tribe 0's town (zone 1) back to zone 2 (one step toward the hub) must be unconditional, even with
        // every segment closed.
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();
        var state = new TribeGuardCorridorState();

        var outcome = TribeGuardCorridorGate.Evaluate(catalog, state, requesterTribe: 1, originZoneId: 1,
            destinationZoneId: 2, requesterIsGmOrAdminRank: false);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void BuildLive_ComposedWithTheGate_SkippingAZoneIsRejectedRegardlessOfGuardState()
    {
        // Jumping from the hub straight into tribe 0's zone 3 (segment 1), skipping zone 4 (segment 0) entirely.
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();
        var state = new TribeGuardCorridorState();
        state.TrySetOpen(0, 1, true); // even with the target segment open, adjacency still fails

        var outcome = TribeGuardCorridorGate.Evaluate(catalog, state, requesterTribe: 1, originZoneId: 38,
            destinationZoneId: 3, requesterIsGmOrAdminRank: false);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }

    [Fact]
    public void BuildLive_ComposedWithTheGate_TheOwningTribeItselfIsNeverGated()
    {
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();
        var state = new TribeGuardCorridorState(); // every segment closed

        var outcome = TribeGuardCorridorGate.Evaluate(catalog, state, requesterTribe: 0, originZoneId: 9999,
            destinationZoneId: 1, requesterIsGmOrAdminRank: false);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }
}
