using Fenrir.Application.Login.Domain.Avatars;

namespace Fenrir.Application.Login.Tests.Avatars;

// Server/Header/mapcheck.h:298-326 (GetReturnBornInTownLocation): switch on Tribe, no default case.
public class RespawnTownCatalogTests
{
    [Theory]
    [InlineData((byte)0, (short)1, 6f, 0f, -7f)]
    [InlineData((byte)1, (short)6, -190f, 0f, 1270f)]
    [InlineData((byte)2, (short)11, 447f, 1f, 440f)]
    [InlineData((byte)3, (short)140, 0f, 0f, -6f)]
    public void TryGetTownLocation_ResolvesTheLiveCoordinatesForEachOfTheFourTribes(byte tribe, short expectedZone,
        float expectedX, float expectedY, float expectedZ)
    {
        var resolved = RespawnTownCatalog.TryGetTownLocation(tribe, out var zoneId, out var x, out var y, out var z);

        Assert.True(resolved);
        Assert.Equal(expectedZone, zoneId);
        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
        Assert.Equal(expectedZ, z);
    }

    [Theory]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    [InlineData((byte)255)]
    public void TryGetTownLocation_UnhandledTribe_ReturnsFalseAndZeroedOutputs(byte tribe)
    {
        var resolved = RespawnTownCatalog.TryGetTownLocation(tribe, out var zoneId, out var x, out var y, out var z);

        Assert.False(resolved);
        Assert.Equal(0, zoneId);
        Assert.Equal(0f, x);
        Assert.Equal(0f, y);
        Assert.Equal(0f, z);
    }

    [Fact]
    public void TryGetTownLocation_MatchesCreateAvatarServicesOwnSpawnMapIdByTribe()
    {
        // Fenrir.Application.Login.Services.CreateAvatar.CreateAvatarService.SpawnMapIdByTribe = [1, 6, 11, 140],
        // independently sourced from the same GetReturnBornInTownLocation citation for the zone-number half only.
        short[] expectedZoneByTribe = [1, 6, 11, 140];

        for (byte tribe = 0; tribe < expectedZoneByTribe.Length; tribe++)
        {
            Assert.True(RespawnTownCatalog.TryGetTownLocation(tribe, out var zoneId, out _, out _, out _));
            Assert.Equal(expectedZoneByTribe[tribe], zoneId);
        }
    }
}
