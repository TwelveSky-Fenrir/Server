using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.World;

public class DungeonServerZoneCatalogTests
{
    [Theory]
    [InlineData(46)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(59)]
    [InlineData(101)]
    [InlineData(126)]
    [InlineData(210)]
    [InlineData(47)]
    [InlineData(60)]
    [InlineData(167)]
    [InlineData(171)]
    [InlineData(219)]
    [InlineData(220)]
    [InlineData(221)]
    [InlineData(39)]
    [InlineData(144)]
    [InlineData(145)]
    [InlineData(313)]
    [InlineData(74)]
    public void IsDungeonServer_KnownServerNumber_ReturnsTrue(short mapId)
    {
        Assert.True(DungeonServerZoneCatalog.IsDungeonServer(mapId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(75)]
    [InlineData(500)]
    public void IsDungeonServer_UnlistedServerNumber_ReturnsFalse(short mapId)
    {
        Assert.False(DungeonServerZoneCatalog.IsDungeonServer(mapId));
    }
}
