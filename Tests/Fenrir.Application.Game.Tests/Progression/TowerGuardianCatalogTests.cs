using Fenrir.Application.Game.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class TowerGuardianCatalogTests
{
    [Theory]
    [InlineData(2, 1, 589)] // Silver Tower[L1]
    [InlineData(4, 1, 590)] // Silver Tower[L2]
    [InlineData(6, 1, 591)] // Silver Tower[L3]
    [InlineData(8, 1, 592)] // Silver Tower[L4]
    [InlineData(2, 2, 593)] // CP Tower[L1]
    [InlineData(4, 2, 594)]
    [InlineData(6, 2, 595)]
    [InlineData(8, 2, 596)]
    [InlineData(2, 3, 597)] // EXP Tower[L1]
    [InlineData(4, 3, 598)]
    [InlineData(6, 3, 599)]
    [InlineData(8, 3, 600)]
    public void ResolveMonsterId_MatchesTheLegacySummonMonsterForTribeTowerFormula(int level, int towerType,
        int expectedMonsterId)
    {
        Assert.Equal(expectedMonsterId, TowerGuardianCatalog.ResolveMonsterId(level, towerType));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 1)]
    [InlineData(2, 0)]
    [InlineData(2, 4)]
    public void ResolveMonsterId_OutOfRangeInputs_ReturnZero(int level, int towerType)
    {
        Assert.Equal(0, TowerGuardianCatalog.ResolveMonsterId(level, towerType));
    }

    [Theory]
    [InlineData((short)2, -1276f, -5f, 1826f)]
    [InlineData((short)143, -67f, -12f, 3046f)]
    public void TryGetGuardianLocation_KnownTowerZone_ReturnsItsFixedStandPoint(short zoneNumber, float expectedX,
        float expectedY, float expectedZ)
    {
        var found = TowerGuardianCatalog.TryGetGuardianLocation(zoneNumber, out var x, out var y, out var z);

        Assert.True(found);
        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
        Assert.Equal(expectedZ, z);
    }

    [Fact]
    public void TryGetGuardianLocation_ZoneWithNoTower_ReturnsFalse()
    {
        var found = TowerGuardianCatalog.TryGetGuardianLocation(999, out _, out _, out _);

        Assert.False(found);
    }
}
