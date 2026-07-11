using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Tests.Simulation;

public sealed class Zone175RewardTablesTests
{
    [Theory]
    [InlineData(1, 100_000_000L)]
    [InlineData(2, 100_000_000L)]
    [InlineData(3, 100_000_000L)]
    [InlineData(4, 100_000_000L)]
    [InlineData(5, 200_000_000L)]
    public void MoneyForStage_IsFixed100MFor1Through4And200MForStage5(int stage, long expected)
    {
        Assert.Equal(expected, Zone175RewardTables.MoneyForStage(stage));
    }

    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 20)]
    [InlineData(3, 50)]
    [InlineData(4, 100)]
    [InlineData(5, 200)]
    public void ContributionPointsForStage_MatchesTheFixedTable(int stage, int expected)
    {
        Assert.Equal(expected, Zone175RewardTables.ContributionPointsForStage(stage));
    }

    [Theory]
    [InlineData(1, 40)]
    [InlineData(2, 41)]
    [InlineData(3, 42)]
    [InlineData(4, 43)]
    [InlineData(5, 44)]
    public void WaveBossSpecialType_Is40Through44(int stage, int expected)
    {
        Assert.Equal((byte)expected, Zone175RewardTables.WaveBossSpecialType(stage));
    }

    [Theory]
    [InlineData(39, false)]
    [InlineData(40, true)]
    [InlineData(44, true)]
    [InlineData(45, false)]
    public void IsWaveBossSpecialType_CoversExactly40Through44(int specialType, bool expected)
    {
        Assert.Equal(expected, Zone175RewardTables.IsWaveBossSpecialType((byte)specialType));
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(1, 0, false)]
    [InlineData(2, 1, false)]
    [InlineData(4, 4, true)]
    [InlineData(4, 3, false)]
    public void CanAdvanceToNextWave_RequiresIndex2AtLeastTheClearedWaveNumber(int clearedWave, int index2,
        bool expected)
    {
        Assert.Equal(expected, Zone175RewardTables.CanAdvanceToNextWave(clearedWave, index2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(13)]
    public void WaveClearExperience_IsZeroToday_TierTableIsAnUnrecoveredGap(int rebirthTier)
    {
        Assert.Equal(0, Zone175RewardTables.WaveClearExperience(rebirthTier, 1f));
        Assert.Equal(0, Zone175RewardTables.WaveClearExperience(rebirthTier, 5f));
    }

    [Fact]
    public void CadenceConstants_AreDerivedFromTheOneMinuteLegacyTick()
    {
        Assert.Equal(120, Zone175RewardTables.OneMinuteLegacyTicks);
        Assert.Equal(120, Zone175RewardTables.PreOpenCountdownCadenceTicks);
        Assert.Equal(60 * 120, Zone175RewardTables.WaveTimeoutLegacyTicks);
        Assert.Equal(60 * 120, Zone175RewardTables.TerminalHoldLegacyTicks);
        Assert.Equal(20, Zone175RewardTables.TrickleCadenceSubTicks);
        Assert.Equal(10, Zone175RewardTables.PreOpenCountStart);
        Assert.Equal(5, Zone175RewardTables.WaveCount);
    }
}
