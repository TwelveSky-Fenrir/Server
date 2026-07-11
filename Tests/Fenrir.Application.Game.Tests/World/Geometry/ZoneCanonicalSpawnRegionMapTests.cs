using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Tests.World.Geometry;

public class ZoneCanonicalSpawnRegionMapTests
{
    [Theory]
    [InlineData(22, 16)]
    [InlineData(28, 16)]
    [InlineData(23, 17)]
    [InlineData(29, 17)]
    [InlineData(24, 18)]
    [InlineData(30, 18)]
    [InlineData(102, 101)]
    [InlineData(103, 101)]
    [InlineData(167, 101)]
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
    [InlineData(344)]
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
    [InlineData(41)]
    [InlineData(211)]
    public void ResolveCanonicalSpawnZoneId_KnownIncompleteGroup_FallsBackToIdentityPendingExtraction(
        short physicalZoneId)
    {
        Assert.Equal(physicalZoneId, ZoneCanonicalSpawnRegionMap.ResolveCanonicalSpawnZoneId(physicalZoneId));
    }
}
