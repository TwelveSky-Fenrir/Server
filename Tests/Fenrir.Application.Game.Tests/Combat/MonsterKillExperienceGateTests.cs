using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class MonsterKillExperienceGateTests
{
    [Fact]
    public void ShouldProcess_NotReady_ReturnsFalseEvenWithPositiveAmounts()
    {
        Assert.False(MonsterKillExperienceGate.ShouldProcess(false, false, 100, 100));
    }

    [Fact]
    public void ShouldProcess_TransferringZone_ReturnsFalseEvenWithPositiveAmounts()
    {
        Assert.False(MonsterKillExperienceGate.ShouldProcess(true, true, 100, 100));
    }

    [Fact]
    public void ShouldProcess_BothAmountsNonPositive_ReturnsFalse()
    {
        Assert.False(MonsterKillExperienceGate.ShouldProcess(true, false, 0, 0));
        Assert.False(MonsterKillExperienceGate.ShouldProcess(true, false, -5, 0));
    }

    [Fact]
    public void ShouldProcess_OnlyCharacterExperiencePositive_ReturnsTrue()
    {
        Assert.True(MonsterKillExperienceGate.ShouldProcess(true, false, 100, 0));
    }

    [Fact]
    public void ShouldProcess_OnlyPetExperiencePositive_ReturnsTrue()
    {
        Assert.True(MonsterKillExperienceGate.ShouldProcess(true, false, 0, 100));
    }
}
