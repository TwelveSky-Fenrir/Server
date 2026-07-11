using System.Numerics;
using Fenrir.Application.Game.Domain.Fishing;
using Fenrir.Application.Game.Domain.World.Geometry;

namespace Fenrir.Application.Game.Tests.Fishing;

public class FishingCastResolverTests
{
    private static ZoneGeometry FlatSquareGeometry()
    {
        var planeInfo = new Vector4(0f, 1f, 0f, 10f);

        var triangles = new[]
        {
            new WorldTriangle(new Vector3(0, 10, 0), new Vector3(100, 10, 0), new Vector3(100, 10, 100), planeInfo),
            new WorldTriangle(new Vector3(0, 10, 0), new Vector3(100, 10, 100), new Vector3(0, 10, 100), planeInfo)
        };

        var root = new QuadtreeNode(new Vector3(0, 0, 0), new Vector3(100, 10, 100), [0, 1], [-1, -1, -1, -1]);

        return new ZoneGeometry(triangles, [root]);
    }

    [Fact]
    public void NoGeometry_NoWater()
    {
        Assert.False(FishingCastResolver.HasWaterAtCurrentPosition(null, 30f, 10f, 20f));
    }

    [Fact]
    public void OnMesh_HasWater()
    {
        var geometry = FlatSquareGeometry();

        Assert.True(FishingCastResolver.HasWaterAtCurrentPosition(geometry, 30f, 10f, 20f));
    }

    [Fact]
    public void OffMesh_NoWater()
    {
        var geometry = FlatSquareGeometry();

        Assert.False(FishingCastResolver.HasWaterAtCurrentPosition(geometry, 200f, 10f, 200f));
    }
}
