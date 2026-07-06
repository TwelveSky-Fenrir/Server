using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="TribeGuardCorridorCatalog" />'s structural bookkeeping (destination -&gt; segment
///     reverse lookup, origin-segment resolution, owning-zone segment sets) against a synthetic two-tribe
///     catalog -- the real sixteen-zone/hub table is not reproduced anywhere in this codebase yet (see that
///     class's own remarks).
/// </summary>
public class TribeGuardCorridorCatalogTests
{
    private const short HubZoneId = 100;

    // Tribe 0's own chain: 1 -> 2 -> 3 -> 4 (home). Tribe 1's own chain: 10 -> 11 -> 12 -> 13 (home).
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

        // Zone 10 belongs to tribe 1's own chain, not tribe 0's -- unrelated from tribe 0's point of view.
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

        var ownedByZone1 = catalog.GetSegmentsOwnedByZone(1); // tribe 0's chain[0]
        Assert.Single(ownedByZone1);
        Assert.Equal(((byte)0, (byte)1), ownedByZone1[0]);

        var ownedByZone2 = catalog.GetSegmentsOwnedByZone(2); // tribe 0's chain[1]
        Assert.Single(ownedByZone2);
        Assert.Equal(((byte)0, (byte)2), ownedByZone2[0]);
    }

    [Fact]
    public void GetSegmentsOwnedByZone_TheHomeZoneOwnsNoSegmentAtAll()
    {
        var catalog = CreateCatalog();

        Assert.Empty(catalog.GetSegmentsOwnedByZone(4)); // tribe 0's chain[3] == home
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
            .Add((0, (byte)0), ImmutableArray.Create(500, 501, 502, 503, 504));
        var catalog = CreateCatalog(slots);

        Assert.True(catalog.TryGetGuardPostSlots(0, 0, out var resolved));
        // .ToArray() -- ImmutableArray<T>'s own IEquatable<T> is backing-array reference equality, not
        // elementwise, so comparing it directly against a collection-expression literal here would take that
        // reference-equality path (via T,T type inference) and spuriously fail despite identical contents.
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
