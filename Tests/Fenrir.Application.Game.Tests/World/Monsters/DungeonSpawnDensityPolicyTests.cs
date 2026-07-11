using Fenrir.Application.Game.Domain.World.Monsters;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class DungeonSpawnDensityPolicyTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(12)]
    [InlineData(19)]
    public void ResolveConfiguredSpawnCount_DungeonZone_CountInBand_MonsterBelow500_BumpsTo20(int configuredCount)
    {
        var result = DungeonSpawnDensityPolicy.ResolveConfiguredSpawnCount(
            true, configuredCount, 100);

        Assert.Equal(DungeonSpawnDensityPolicy.BumpedSpawnCount, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void ResolveConfiguredSpawnCount_DungeonZone_CountBelowBand_LeavesUnchanged(int configuredCount)
    {
        var result = DungeonSpawnDensityPolicy.ResolveConfiguredSpawnCount(
            true, configuredCount, 100);

        Assert.Equal(configuredCount, result);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(9999)]
    public void ResolveConfiguredSpawnCount_DungeonZone_CountAtOrAboveBand_LeavesUnchanged(int configuredCount)
    {
        var result = DungeonSpawnDensityPolicy.ResolveConfiguredSpawnCount(
            true, configuredCount, 100);

        Assert.Equal(configuredCount, result);
    }

    [Fact]
    public void ResolveConfiguredSpawnCount_DungeonZone_CountInBand_MonsterAt500_LeavesUnchanged()
    {
        var result = DungeonSpawnDensityPolicy.ResolveConfiguredSpawnCount(
            true, 10, 500);

        Assert.Equal(10, result);
    }

    [Fact]
    public void ResolveConfiguredSpawnCount_DungeonZone_CountInBand_MonsterAt499_BumpsTo20()
    {
        var result = DungeonSpawnDensityPolicy.ResolveConfiguredSpawnCount(
            true, 10, 499);

        Assert.Equal(DungeonSpawnDensityPolicy.BumpedSpawnCount, result);
    }

    [Fact]
    public void ResolveConfiguredSpawnCount_DungeonZone_CountInBand_MonsterWellAbove500_LeavesUnchanged()
    {
        var result = DungeonSpawnDensityPolicy.ResolveConfiguredSpawnCount(
            true, 10, 12345);

        Assert.Equal(10, result);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(19)]
    public void ResolveConfiguredSpawnCount_NonDungeonZone_NeverBumps(int configuredCount)
    {
        var result = DungeonSpawnDensityPolicy.ResolveConfiguredSpawnCount(
            false, configuredCount, 1);

        Assert.Equal(configuredCount, result);
    }

    [Fact]
    public void ResolveTableCapacity_DungeonZone_DoublesBaseCapacity()
    {
        var result = DungeonSpawnDensityPolicy.ResolveTableCapacity(true, 3400);

        Assert.Equal(6800, result);
    }

    [Fact]
    public void ResolveTableCapacity_NonDungeonZone_LeavesBaseCapacityUnchanged()
    {
        var result = DungeonSpawnDensityPolicy.ResolveTableCapacity(false, 3400);

        Assert.Equal(3400, result);
    }
}
