using Fenrir.Application.Game.Domain.World.Geometry;

namespace Fenrir.Application.Game.Tests.World.Geometry;

public class ZoneCanonicalGeometryMapTests
{
    [Theory]
    [InlineData(176, 175)]
    [InlineData(193, 175)]
    [InlineData(19, 175)]
    [InlineData(34, 175)]
    [InlineData(36, 175)]
    [InlineData(22, 16)]
    [InlineData(28, 16)]
    [InlineData(23, 17)]
    [InlineData(30, 18)]
    [InlineData(42, 40)]
    [InlineData(45, 43)]
    [InlineData(48, 46)]
    [InlineData(68, 62)]
    [InlineData(70, 64)]
    [InlineData(79, 76)]
    [InlineData(83, 80)]
    [InlineData(167, 101)]
    [InlineData(105, 104)]
    [InlineData(117, 104)]
    [InlineData(251, 104)]
    [InlineData(266, 104)]
    [InlineData(127, 126)]
    [InlineData(221, 126)]
    [InlineData(39, 126)]
    [InlineData(144, 126)]
    [InlineData(145, 126)]
    [InlineData(313, 126)]
    [InlineData(233, 222)]
    [InlineData(120, 154)]
    [InlineData(296, 154)]
    [InlineData(157, 154)]
    [InlineData(160, 154)]
    [InlineData(199, 195)]
    [InlineData(85, 195)]
    [InlineData(336, 310)]
    public void ResolveCanonicalMapId_RemappedZone_ReturnsCanonical(short physical, short canonical)
    {
        Assert.Equal(canonical, ZoneCanonicalGeometryMap.ResolveCanonicalMapId(physical));
    }

    [Theory]
    [InlineData(175)]
    [InlineData(104)]
    [InlineData(126)]
    [InlineData(154)]
    [InlineData(195)]
    [InlineData(310)]
    [InlineData(16)]
    [InlineData(1)]
    [InlineData(38)]
    [InlineData(49)]
    [InlineData(500)]
    public void ResolveCanonicalMapId_UnmappedOrCanonicalZone_ReturnsInput(short mapId)
    {
        Assert.Equal(mapId, ZoneCanonicalGeometryMap.ResolveCanonicalMapId(mapId));
    }
}
