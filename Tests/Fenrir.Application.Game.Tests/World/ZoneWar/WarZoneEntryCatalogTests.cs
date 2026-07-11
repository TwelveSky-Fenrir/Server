using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="WarZoneEntryCatalog" />'s frozen table in isolation -- the exact cited numbers per
///     zone, and that the rebirth-tier split for 295/296/322/323 partitions correctly (no overlap, no gap at the
///     boundary).
/// </summary>
public class WarZoneEntryCatalogTests
{
    [Fact]
    public void Zone164_RequiresExactMaxLevelAndRebirthZeroToSix()
    {
        Assert.True(WarZoneEntryCatalog.TryGetRule(164, out var rule));
        Assert.Equal(RebirthProgression.CombinedLevelCap, rule.MinCombinedLevel);
        Assert.Equal(RebirthProgression.CombinedLevelCap, rule.MaxCombinedLevel);
        Assert.Equal(0, rule.MinRebirthCount);
        Assert.Equal(6, rule.MaxRebirthCount);
    }

    [Theory]
    [InlineData((short)295)]
    [InlineData((short)322)]
    public void LowRebirthTierZones_RequireExactMaxLevelAndRebirthZeroToSix(short zoneNumber)
    {
        Assert.True(WarZoneEntryCatalog.TryGetRule(zoneNumber, out var rule));
        Assert.Equal(RebirthProgression.CombinedLevelCap, rule.MinCombinedLevel);
        Assert.Equal(RebirthProgression.CombinedLevelCap, rule.MaxCombinedLevel);
        Assert.Equal(0, rule.MinRebirthCount);
        Assert.Equal(6, rule.MaxRebirthCount);
    }

    [Theory]
    [InlineData((short)296)]
    [InlineData((short)323)]
    public void HighRebirthTierZones_RequireExactMaxLevelAndRebirthSevenAndAbove(short zoneNumber)
    {
        Assert.True(WarZoneEntryCatalog.TryGetRule(zoneNumber, out var rule));
        Assert.Equal(RebirthProgression.CombinedLevelCap, rule.MinCombinedLevel);
        Assert.Equal(RebirthProgression.CombinedLevelCap, rule.MaxCombinedLevel);
        Assert.Equal(7, rule.MinRebirthCount);
        Assert.Equal(RebirthProgression.MaxRebirthGeneration, rule.MaxRebirthCount);
    }

    [Fact]
    public void Zone335_FfaArena_RequiresLevelBand145To157AndRebirthZeroToTwelve()
    {
        Assert.True(WarZoneEntryCatalog.TryGetRule(335, out var rule));
        Assert.Equal(145, rule.MinCombinedLevel);
        Assert.Equal(RebirthProgression.CombinedLevelCap, rule.MaxCombinedLevel);
        Assert.Equal(0, rule.MinRebirthCount);
        Assert.Equal(12, rule.MaxRebirthCount);
    }

    [Fact]
    public void LowAndHighRebirthTiers_PartitionExactlyAtSeven_NoOverlapNoGap()
    {
        Assert.True(WarZoneEntryCatalog.TryGetRule(295, out var low));
        Assert.True(WarZoneEntryCatalog.TryGetRule(296, out var high));

        Assert.Equal(6, low.MaxRebirthCount);
        Assert.Equal(7, high.MinRebirthCount);
    }

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)38)]
    [InlineData((short)124)]
    [InlineData((short)251)] // Odawa custom zone -- deliberately not in this table, see catalog remarks
    [InlineData((short)266)]
    [InlineData((short)319)] // unconditional-pass auto-hunt zone, a DIFFERENT routine -- not this gate's table
    public void ZonesOutsideTheCitedSet_HaveNoRule(short zoneNumber)
    {
        Assert.False(WarZoneEntryCatalog.TryGetRule(zoneNumber, out _));
    }

    [Fact]
    public void Rules_ContainsExactlyTheSixCitedZonesAndNothingElse()
    {
        short[] expected = [164, 295, 296, 322, 323, 335];

        Assert.Equal(expected.Length, WarZoneEntryCatalog.Rules.Count);
        foreach (var zoneNumber in expected)
            Assert.True(WarZoneEntryCatalog.Rules.ContainsKey(zoneNumber));
    }
}
