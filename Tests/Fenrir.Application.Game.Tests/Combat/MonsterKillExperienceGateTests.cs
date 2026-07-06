using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

/// <summary>
///     Covers <see cref="MonsterKillExperienceGate" /> against <c>MyUtil::ProcessForExperience</c>'s outer
///     guard (S07_MyGame03.cpp:163-166).
/// </summary>
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
