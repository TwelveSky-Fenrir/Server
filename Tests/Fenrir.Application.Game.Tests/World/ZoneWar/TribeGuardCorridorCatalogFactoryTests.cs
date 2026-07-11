using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeGuardCorridorCatalogFactoryTests
{
    [Fact]
    public void BuildLive_HubZoneIs38()
    {
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();

        Assert.Equal((short)38, catalog.HubZoneId);
    }

    [Theory]
    [InlineData((short)4, (byte)0, (byte)0)]
    [InlineData((short)3, (byte)0, (byte)1)]
    [InlineData((short)2, (byte)0, (byte)2)]
    [InlineData((short)1, (byte)0, (byte)3)]
    [InlineData((short)9, (byte)1, (byte)0)]
    [InlineData((short)8, (byte)1, (byte)1)]
    [InlineData((short)7, (byte)1, (byte)2)]
    [InlineData((short)6, (byte)1, (byte)3)]
    [InlineData((short)14, (byte)2, (byte)0)]
    [InlineData((short)13, (byte)2, (byte)1)]
    [InlineData((short)12, (byte)2, (byte)2)]
    [InlineData((short)11, (byte)2, (byte)3)]
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
    [InlineData((short)4, (byte)0, (byte)1)]
    [InlineData((short)3, (byte)0, (byte)2)]
    [InlineData((short)2, (byte)0, (byte)3)]
    public void BuildLive_ACorridorZoneOwnsOnlyTheNextSegmentOfItsOwnTribe(short zoneId, byte expectedTribe,
        byte expectedSegment)
    {
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();

        var owned = catalog.GetSegmentsOwnedByZone(zoneId);

        Assert.Single(owned);
        Assert.Equal((expectedTribe, expectedSegment), owned[0]);
    }

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)6)]
    [InlineData((short)11)]
    [InlineData((short)140)]
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
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();
        var state = new TribeGuardCorridorState();

        var blocked = TribeGuardCorridorGate.Evaluate(catalog, state, 1, 2,
            1, false);
        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, blocked);

        state.TrySetOpen(0, 3, true);

        var allowed = TribeGuardCorridorGate.Evaluate(catalog, state, 1, 2,
            1, false);
        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, allowed);
    }

    [Fact]
    public void BuildLive_ComposedWithTheGate_EnemyRetreatingTowardTheHubIsAlwaysAllowed()
    {
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();
        var state = new TribeGuardCorridorState();

        var outcome = TribeGuardCorridorGate.Evaluate(catalog, state, 1, 1,
            2, false);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void BuildLive_ComposedWithTheGate_SkippingAZoneIsRejectedRegardlessOfGuardState()
    {
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();
        var state = new TribeGuardCorridorState();
        state.TrySetOpen(0, 1, true);

        var outcome = TribeGuardCorridorGate.Evaluate(catalog, state, 1, 38,
            3, false);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }

    [Fact]
    public void BuildLive_ComposedWithTheGate_TheOwningTribeItselfIsNeverGated()
    {
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();
        var state = new TribeGuardCorridorState();

        var outcome = TribeGuardCorridorGate.Evaluate(catalog, state, 0, 9999,
            1, false);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }
}
