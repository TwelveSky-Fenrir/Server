using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

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

    [Fact]
    public void Zone322_RequiresExactMaxLevelAndRebirthZeroToSix()
    {
        Assert.True(WarZoneEntryCatalog.TryGetRule(322, out var rule));
        Assert.Equal(RebirthProgression.CombinedLevelCap, rule.MinCombinedLevel);
        Assert.Equal(RebirthProgression.CombinedLevelCap, rule.MaxCombinedLevel);
        Assert.Equal(0, rule.MinRebirthCount);
        Assert.Equal(6, rule.MaxRebirthCount);
    }

    [Fact]
    public void Zone295_IsRebirthOnlyWithNoCombinedLevelRequirement()
    {
        Assert.True(WarZoneEntryCatalog.TryGetRule(295, out var rule));
        Assert.Null(rule.MinCombinedLevel);
        Assert.Null(rule.MaxCombinedLevel);
        Assert.False(rule.HasCombinedLevelRequirement);
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
    [InlineData((short)250)]
    [InlineData((short)267)]
    [InlineData((short)319)]
    public void ZonesOutsideTheCitedSet_HaveNoRule(short zoneNumber)
    {
        Assert.False(WarZoneEntryCatalog.TryGetRule(zoneNumber, out _));
    }

    [Fact]
    public void Rules_ContainsExactlyTheCitedZonesAndNothingElse()
    {
        short[] expected =
        [
            164, 295, 296, 322, 323, 335,
            251, 252, 253, 254, 255, 256, 257, 258, 259, 260, 261, 262, 263, 264, 265, 266
        ];

        Assert.Equal(expected.Length, WarZoneEntryCatalog.Rules.Count);
        foreach (var zoneNumber in expected)
            Assert.True(WarZoneEntryCatalog.Rules.ContainsKey(zoneNumber));
    }

    [Theory]
    [InlineData((short)251)]
    [InlineData((short)258)]
    [InlineData((short)266)]
    public void OdawaCaveZones_RequireCombinedLevel146To157WithNoRebirthRestriction(short zoneNumber)
    {
        Assert.True(WarZoneEntryCatalog.TryGetRule(zoneNumber, out var rule));
        Assert.Equal(146, rule.MinCombinedLevel);
        Assert.Equal(157, rule.MaxCombinedLevel);
        Assert.Equal(0, rule.MinRebirthCount);
        Assert.Equal(RebirthProgression.MaxRebirthGeneration, rule.MaxRebirthCount);
    }

    [Fact]
    public void OdawaCaveRange_Covers251Through266Inclusive_WithUniformBounds()
    {
        for (short zoneNumber = 251; zoneNumber <= 266; zoneNumber++)
        {
            Assert.True(WarZoneEntryCatalog.TryGetRule(zoneNumber, out var rule));
            Assert.Equal(146, rule.MinCombinedLevel);
            Assert.Equal(157, rule.MaxCombinedLevel);
        }
    }
}
