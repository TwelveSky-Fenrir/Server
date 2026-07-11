using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class Zone195NokSanSiteTests
{
    [Fact]
    public void DefaultConstruction_UsesRecoveredCapturePostLiteralsAndDefaultRadius()
    {
        var site = new Zone195NokSanSite(196, 0, 196);

        Assert.Equal(Zone195NokSanState.DefaultPostX, site.PostX);
        Assert.Equal(Zone195NokSanState.DefaultPostZ, site.PostZ);
        Assert.Equal(Zone195NokSanState.DefaultCaptureRadius, site.CaptureRadius);
    }

    [Fact]
    public void RecoveredDefaults_MatchTheCitedLegacyLiterals()
    {
        Assert.Equal(-20.0f, Zone195NokSanState.DefaultPostX);
        Assert.Equal(2510.0f, Zone195NokSanState.DefaultPostZ);
    }

    [Fact]
    public void AllThreeLiveShards_ShareTheIdenticalCapturePost_WhenConstructedWithDefaults()
    {
        var server196 = new Zone195NokSanSite(1, 0, 196);
        var server99 = new Zone195NokSanSite(2, 2, 99);
        var server100 = new Zone195NokSanSite(3, 3, 100);

        Assert.Equal(server196.PostX, server99.PostX);
        Assert.Equal(server196.PostX, server100.PostX);
        Assert.Equal(server196.PostZ, server99.PostZ);
        Assert.Equal(server196.PostZ, server100.PostZ);
    }

    [Fact]
    public void ExplicitPostCoordinates_OverrideTheDefaults()
    {
        var site = new Zone195NokSanSite(196, 0, 196, 500f, -750f);

        Assert.Equal(500f, site.PostX);
        Assert.Equal(-750f, site.PostZ);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public void IsRewardWindowShard_OnlyTrueForStoneSlotZero(int stoneSlotIndex, bool expected)
    {
        var site = new Zone195NokSanSite(196, stoneSlotIndex, 196);

        Assert.Equal(expected, site.IsRewardWindowShard);
    }
}
