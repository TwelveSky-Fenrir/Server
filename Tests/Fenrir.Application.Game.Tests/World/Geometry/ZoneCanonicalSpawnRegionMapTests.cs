using Fenrir.Application.Game.Domain.World.Geometry;

namespace Fenrir.Application.Game.Tests.World.Geometry;

/// <summary>
///     Covers <see cref="ZoneCanonicalSpawnRegionMap" /> against the legacy <c>MySummon::Init</c>
///     <c>mSameSummon</c> remap and <c>_FIX</c> filename-suffix override
///     (<c>Server/ts25zone/S10_MySummon.cpp:88-360</c>). Only the fully-specified Table B groups from
///     <c>wave11/A1-canonical-wm-remap.md</c> are asserted as "correct"; the not-yet-extracted groups are
///     asserted as the documented, known-incomplete identity fallback -- see <see cref="ZoneCanonicalSpawnRegionMap" />'s
///     own remarks for the exact list of anchors still pending extraction.
/// </summary>
public class ZoneCanonicalSpawnRegionMapTests
{
    [Theory]
    // Cave/ruin/tomb triples (:101-117).
    [InlineData(22, 16)]
    [InlineData(28, 16)]
    [InlineData(23, 17)]
    [InlineData(29, 17)]
    [InlineData(24, 18)]
    [InlineData(30, 18)]
    // 101 four-zone group (:167-172).
    [InlineData(102, 101)]
    [InlineData(103, 101)]
    [InlineData(167, 101)]
    // 126-129 four sub-groups (:230-256).
    [InlineData(130, 126)]
    [InlineData(134, 126)]
    [InlineData(171, 126)]
    [InlineData(131, 127)]
    [InlineData(135, 127)]
    [InlineData(172, 127)]
    [InlineData(132, 128)]
    [InlineData(136, 128)]
    [InlineData(173, 128)]
    [InlineData(133, 129)]
    [InlineData(137, 129)]
    [InlineData(174, 129)]
    // 310 canonical (:351-353) -- matches ZoneCanonicalGeometryMap's own 310/336 group (the tables happen to
    // agree here; that is not true in general, see the _FIX case below).
    [InlineData(336, 310)]
    public void ResolveCanonicalSpawnZoneId_FullySpecifiedGroup_ReturnsCanonical(short physical, short canonical)
    {
        Assert.Equal(canonical, ZoneCanonicalSpawnRegionMap.ResolveCanonicalSpawnZoneId(physical));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(101)]
    [InlineData(126)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(310)]
    [InlineData(344)] // explicit self-mapping in the legacy switch, still a no-op here.
    // ...as does any id absent from the switch (the legacy `default` keeps zoneNumber == mServerNumber).
    [InlineData(1)]
    [InlineData(500)]
    public void ResolveCanonicalSpawnZoneId_CanonicalOrUnmappedZone_ReturnsInput(short zoneId)
    {
        Assert.Equal(zoneId, ZoneCanonicalSpawnRegionMap.ResolveCanonicalSpawnZoneId(zoneId));
    }

    [Theory]
    [InlineData(39)]
    [InlineData(144)]
    [InlineData(145)]
    [InlineData(313)]
    [InlineData(74)]
    public void ResolveCanonicalSpawnZoneId_FixSuffixZone_KeepsOwnZoneNumberUnchanged(short physicalZoneId)
    {
        // The _FIX case exits the legacy selector before any value-changing group runs -- unlike the navmesh
        // table (ZoneCanonicalGeometryMap), which DOES fold 39/144/145/313 into canonical 126.
        Assert.Equal(physicalZoneId, ZoneCanonicalSpawnRegionMap.ResolveCanonicalSpawnZoneId(physicalZoneId));
    }

    [Theory]
    [InlineData(39, true)]
    [InlineData(144, true)]
    [InlineData(145, true)]
    [InlineData(313, true)]
    [InlineData(74, true)]
    [InlineData(16, false)]
    [InlineData(101, false)]
    [InlineData(126, false)]
    [InlineData(500, false)]
    public void UsesFixSuffix_ReturnsExpectedFlag(short physicalZoneId, bool expected)
    {
        Assert.Equal(expected, ZoneCanonicalSpawnRegionMap.UsesFixSuffix(physicalZoneId));
    }

    [Fact]
    public void Resolve_FixSuffixZone_ReturnsOwnIdAndFixFlag()
    {
        var resolved = ZoneCanonicalSpawnRegionMap.Resolve(39);

        Assert.Equal((short)39, resolved.CanonicalZoneId);
        Assert.True(resolved.UsesFixSuffix);
    }

    [Fact]
    public void Resolve_RemappedNonFixZone_ReturnsCanonicalIdAndNoFixFlag()
    {
        var resolved = ZoneCanonicalSpawnRegionMap.Resolve(102);

        Assert.Equal((short)101, resolved.CanonicalZoneId);
        Assert.False(resolved.UsesFixSuffix);
    }

    [Theory]
    // Known-incomplete groups: the contract names only the canonical anchor + a cardinality description, never
    // the member-zone list, for these clusters (S10_MySummon.cpp:119-165, 174-228, 258-307, 310-329, 331-350).
    // These physical zones are NOT the canonical anchor of their own (presumed) group, so a complete table
    // would remap them -- but per the "never invent membership data" rule this map deliberately leaves them at
    // the safe identity fallback until a follow-up extraction closes the gap. This test documents that known
    // limitation so it fails loudly (a useful signal, not a regression) the moment someone fills the group in
    // without updating this test.
    [InlineData(41)] // presumed sibling of anchor 40 by analogy only -- never confirmed, must stay unmapped.
    [InlineData(211)] // presumed Temple Exterior variant of anchor 210 -- never confirmed.
    public void ResolveCanonicalSpawnZoneId_KnownIncompleteGroup_FallsBackToIdentityPendingExtraction(
        short physicalZoneId)
    {
        Assert.Equal(physicalZoneId, ZoneCanonicalSpawnRegionMap.ResolveCanonicalSpawnZoneId(physicalZoneId));
    }
}
