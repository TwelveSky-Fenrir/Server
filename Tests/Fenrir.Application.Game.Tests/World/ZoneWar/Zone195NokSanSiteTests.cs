using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="Zone195NokSanSite" />'s recovered capture-post defaults -- the fixed world-space
///     X/Z literal (Server/ts25zone/S07_MyGame01.cpp:1148,1150) shared unconditionally by all three live
///     Nok-San shards, plus the pre-existing <see cref="Zone195NokSanState.DefaultCaptureRadius" /> default
///     and <see cref="Zone195NokSanSite.IsRewardWindowShard" /> gate.
/// </summary>
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
        // Server/ts25zone/S07_MyGame01.cpp:1148 (X), :1150 (Z) -- one shared literal pair across all shards.
        Assert.Equal(-20.0f, Zone195NokSanState.DefaultPostX);
        Assert.Equal(2510.0f, Zone195NokSanState.DefaultPostZ);
    }

    [Fact]
    public void AllThreeLiveShards_ShareTheIdenticalCapturePost_WhenConstructedWithDefaults()
    {
        // Server 196 -> slot 0, server 99 -> slot 2, server 100 -> slot 3
        // (Server/ts25zone/S07_MyGame01.cpp:1140-1176) -- the post location is written once, before the
        // per-server-number switch, so it does not vary by slot.
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
        // Operator configuration may still override the post location (e.g. a private test-deployment
        // shard) -- the recovered values are defaults, not a hardcoded constant on the record itself.
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
