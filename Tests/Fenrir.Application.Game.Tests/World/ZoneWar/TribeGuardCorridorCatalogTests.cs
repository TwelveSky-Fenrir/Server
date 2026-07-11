using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeGuardCorridorCatalogTests
{
    private const short HubZoneId = 100;

    private static readonly ImmutableArray<short> Tribe0Chain = [1, 2, 3, 4];
    private static readonly ImmutableArray<short> Tribe1Chain = [10, 11, 12, 13];

    private static TribeGuardCorridorCatalog CreateCatalog(
        ImmutableDictionary<(byte, byte), ImmutableArray<int>>? guardPostSlots = null)
    {
        var chains = new Dictionary<byte, TribeGuardCorridorChain>
        {
            [0] = new(Tribe0Chain),
            [1] = new(Tribe1Chain)
        }.ToImmutableDictionary();

        return new TribeGuardCorridorCatalog(HubZoneId, chains,
            guardPostSlots ?? ImmutableDictionary<(byte, byte), ImmutableArray<int>>.Empty);
    }

    [Fact]
    public void TryGetSegmentForDestinationZone_ResolvesEveryChainZoneToItsOwnTribeAndSegment()
    {
        var catalog = CreateCatalog();

        Assert.True(catalog.TryGetSegmentForDestinationZone(1, out var tribe, out var segment));
        Assert.Equal((byte)0, tribe);
        Assert.Equal((byte)0, segment);

        Assert.True(catalog.TryGetSegmentForDestinationZone(3, out tribe, out segment));
        Assert.Equal((byte)0, tribe);
        Assert.Equal((byte)2, segment);

        Assert.True(catalog.TryGetSegmentForDestinationZone(13, out tribe, out segment));
        Assert.Equal((byte)1, tribe);
        Assert.Equal((byte)3, segment);
    }

    [Fact]
    public void TryGetSegmentForDestinationZone_MissesForTheHubAndForAnyUnrelatedZone()
    {
        var catalog = CreateCatalog();

        Assert.False(catalog.TryGetSegmentForDestinationZone(HubZoneId, out _, out _));
        Assert.False(catalog.TryGetSegmentForDestinationZone(9999, out _, out _));
    }

    [Fact]
    public void GetOriginSegmentIndex_HubIsAlwaysMinusOne_RegardlessOfWhichTribeIsAsked()
    {
        var catalog = CreateCatalog();

        Assert.Equal(-1, catalog.GetOriginSegmentIndex(0, HubZoneId));
        Assert.Equal(-1, catalog.GetOriginSegmentIndex(1, HubZoneId));
    }

    [Fact]
    public void GetOriginSegmentIndex_ResolvesAZoneOnlyAgainstItsOwnTribesChain()
    {
        var catalog = CreateCatalog();

        Assert.Equal(0, catalog.GetOriginSegmentIndex(0, 1));
        Assert.Equal(2, catalog.GetOriginSegmentIndex(0, 3));

        Assert.Null(catalog.GetOriginSegmentIndex(0, 10));
    }

    [Fact]
    public void GetSegmentsOwnedByZone_HubOwnsEveryTribesSegmentZeroInOnePass()
    {
        var catalog = CreateCatalog();

        var owned = catalog.GetSegmentsOwnedByZone(HubZoneId);

        Assert.Equal(2, owned.Count);
        Assert.Contains(((byte)0, (byte)0), owned);
        Assert.Contains(((byte)1, (byte)0), owned);
    }

    [Fact]
    public void GetSegmentsOwnedByZone_ACorridorZoneOwnsOnlyTheNextSegmentOfItsOwnTribe()
    {
        var catalog = CreateCatalog();

        var ownedByZone1 = catalog.GetSegmentsOwnedByZone(1);
        Assert.Single(ownedByZone1);
        Assert.Equal(((byte)0, (byte)1), ownedByZone1[0]);

        var ownedByZone2 = catalog.GetSegmentsOwnedByZone(2);
        Assert.Single(ownedByZone2);
        Assert.Equal(((byte)0, (byte)2), ownedByZone2[0]);
    }

    [Fact]
    public void GetSegmentsOwnedByZone_TheHomeZoneOwnsNoSegmentAtAll()
    {
        var catalog = CreateCatalog();

        Assert.Empty(catalog.GetSegmentsOwnedByZone(4));
    }

    [Fact]
    public void GetSegmentsOwnedByZone_AnUnrelatedMapOwnsNothing()
    {
        var catalog = CreateCatalog();

        Assert.Empty(catalog.GetSegmentsOwnedByZone(9999));
    }

    [Fact]
    public void TryGetGuardPostSlots_MissesUntilConfigured()
    {
        var catalog = CreateCatalog();

        Assert.False(catalog.TryGetGuardPostSlots(0, 0, out _));
    }

    [Fact]
    public void TryGetGuardPostSlots_ReturnsTheConfiguredFiveSlotIndices()
    {
        var slots = ImmutableDictionary<(byte, byte), ImmutableArray<int>>.Empty
            .Add((0, 0), ImmutableArray.Create(500, 501, 502, 503, 504));
        var catalog = CreateCatalog(slots);

        Assert.True(catalog.TryGetGuardPostSlots(0, 0, out var resolved));
        Assert.Equal([500, 501, 502, 503, 504], resolved.ToArray());
    }

    [Fact]
    public void Empty_HasNoHubAndResolvesNothing()
    {
        var catalog = TribeGuardCorridorCatalog.Empty;

        Assert.False(catalog.TryGetSegmentForDestinationZone(1, out _, out _));
        Assert.Empty(catalog.GetSegmentsOwnedByZone(0));
        Assert.Null(catalog.GetOriginSegmentIndex(0, 1));
    }
}
