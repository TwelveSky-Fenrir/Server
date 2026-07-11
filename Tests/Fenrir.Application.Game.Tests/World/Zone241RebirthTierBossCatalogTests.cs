using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.World;

public class Zone241RebirthTierBossCatalogTests
{
    [Theory]
    [InlineData(0, 725)]
    [InlineData(1, 725)]
    [InlineData(2, 726)]
    [InlineData(3, 727)]
    [InlineData(4, 728)]
    [InlineData(5, 729)]
    [InlineData(6, 730)]
    [InlineData(7, 736)]
    [InlineData(8, 737)]
    [InlineData(9, 738)]
    [InlineData(10, 748)]
    [InlineData(11, 749)]
    [InlineData(12, 750)]
    [InlineData(13, 750)]
    public void TryGetBossMonsterId_ResolvesCatalogETable(int rebirthTier, int expectedBossId)
    {
        var resolved = Zone241RebirthTierBossCatalog.Instance.TryGetBossMonsterId(rebirthTier, out var monsterId);

        Assert.True(resolved);
        Assert.Equal(expectedBossId, monsterId);
    }

    [Fact]
    public void TryGetBossMonsterId_NeverFails_UnlikeNullCatalog()
    {
        Assert.True(Zone241RebirthTierBossCatalog.Instance.TryGetBossMonsterId(0, out _));
        Assert.False(NullPersonalDungeonBossCatalog.Instance.TryGetBossMonsterId(0, out _));
    }
}
