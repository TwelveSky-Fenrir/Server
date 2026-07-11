using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class MonsterKillHealthGainCalculatorTests
{
    [Theory]
    [InlineData(1000, 10)]
    [InlineData(150, 1)]
    [InlineData(99, 0)]
    [InlineData(0, 0)]
    public void ComputeHealthValueGain_IsOneHundredthOfMonsterLife(int monsterLife, int expected)
    {
        Assert.Equal(expected, MonsterKillHealthGainCalculator.ComputeHealthValueGain(monsterLife));
    }

    [Fact]
    public void ComputeNewLife_AvatarAlreadyDead_StaysUnchanged()
    {
        Assert.Equal(0, MonsterKillHealthGainCalculator.ComputeNewLife(0, 1000, 50));
        Assert.Equal(-10, MonsterKillHealthGainCalculator.ComputeNewLife(-10, 1000, 50));
    }

    [Fact]
    public void ComputeNewLife_GainWithinMax_AddsTheFullGain()
    {
        Assert.Equal(550, MonsterKillHealthGainCalculator.ComputeNewLife(500, 1000, 50));
    }

    [Fact]
    public void ComputeNewLife_GainWouldExceedMax_ClampsExactlyToMax()
    {
        Assert.Equal(1000, MonsterKillHealthGainCalculator.ComputeNewLife(980, 1000, 50));
    }

    [Fact]
    public void ComputeNewLife_AlreadyAtMax_StaysUnchanged()
    {
        Assert.Equal(1000, MonsterKillHealthGainCalculator.ComputeNewLife(1000, 1000, 50));
    }

    [Fact]
    public void ComputeNewLife_ZeroGain_StaysUnchanged()
    {
        Assert.Equal(500, MonsterKillHealthGainCalculator.ComputeNewLife(500, 1000, 0));
    }
}
