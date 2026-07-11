using Fenrir.Application.Game.Domain.Pets;

namespace Fenrir.Application.Game.Tests.Pets;

public class DoublePetExpTimerGateTests
{
    private const int Tier0Max = 40_000_000;
    private const int Tier1Max = 80_000_000;
    private const int Tier3Max = 320_000_000;

    [Fact]
    public void ResolveGrowthPercent_UnrecognizedItemId_ReturnsZero()
    {
        Assert.Equal(0f, DoublePetExpTimerGate.ResolveGrowthPercent(999_999, Tier0Max * 2));
    }

    [Fact]
    public void ResolveGrowthPercent_LegacyCategory0Id_MatchesFormula()
    {
        Assert.Equal(100f, DoublePetExpTimerGate.ResolveGrowthPercent(541, Tier0Max));
        Assert.Equal(200f, DoublePetExpTimerGate.ResolveGrowthPercent(541, Tier0Max * 2));
    }

    [Fact]
    public void ResolveGrowthPercent_GiftEventCategory0Id_ResolvesSameAsLegacyId()
    {
        Assert.Equal(100f, DoublePetExpTimerGate.ResolveGrowthPercent(8202, Tier0Max));
    }

    [Fact]
    public void ResolveGrowthPercent_GiftEventCategory1Id_UsesCategory1TierMax()
    {
        Assert.Equal(100f, DoublePetExpTimerGate.ResolveGrowthPercent(8211, Tier1Max));
        Assert.NotEqual(100f, DoublePetExpTimerGate.ResolveGrowthPercent(8211, Tier0Max));
    }

    [Fact]
    public void ResolveGrowthPercent_Category3IdIncludingHighLegacyIds_MatchesFormula()
    {
        Assert.Equal(100f, DoublePetExpTimerGate.ResolveGrowthPercent(2160, Tier3Max));
        Assert.Equal(100f, DoublePetExpTimerGate.ResolveGrowthPercent(17057, Tier3Max));
    }

    [Fact]
    public void IsAtFreezeThreshold_BelowTwoHundredPercent_ReturnsFalse()
    {
        Assert.False(DoublePetExpTimerGate.IsAtFreezeThreshold(541, Tier0Max));
    }

    [Fact]
    public void IsAtFreezeThreshold_AtExactlyTwoHundredPercent_ReturnsTrue()
    {
        Assert.True(DoublePetExpTimerGate.IsAtFreezeThreshold(541, Tier0Max * 2));
    }

    [Fact]
    public void IsAtFreezeThreshold_AboveTwoHundredPercent_StillReturnsTrue()
    {
        Assert.True(DoublePetExpTimerGate.IsAtFreezeThreshold(541, Tier0Max * 3));
    }

    [Fact]
    public void IsAtFreezeThreshold_UnrecognizedItemId_NeverFreezes()
    {
        Assert.False(DoublePetExpTimerGate.IsAtFreezeThreshold(999_999, int.MaxValue));
    }

    [Fact]
    public void IsAtFreezeThreshold_NoPetEquipped_TreatedSameAsUnrecognized()
    {
        Assert.False(DoublePetExpTimerGate.IsAtFreezeThreshold(0, int.MaxValue));
    }

    [Fact]
    public void ComputeNextTimerValue_BelowTwoHundredPercent_DrainsByOne()
    {
        Assert.Equal(9, DoublePetExpTimerGate.ComputeNextTimerValue(541, Tier0Max, currentTimerValue: 10));
    }

    [Fact]
    public void ComputeNextTimerValue_AtTwoHundredPercent_LeavesTimerUntouched()
    {
        Assert.Equal(10, DoublePetExpTimerGate.ComputeNextTimerValue(541, Tier0Max * 2, currentTimerValue: 10));
    }

    [Fact]
    public void ComputeNextTimerValue_NoPetEquipped_StillDrainsByOne()
    {
        Assert.Equal(9, DoublePetExpTimerGate.ComputeNextTimerValue(0, 0, currentTimerValue: 10));
    }

    [Fact]
    public void ComputeNextTimerValue_TimerAlreadyZero_IsANoOp()
    {
        Assert.Equal(0, DoublePetExpTimerGate.ComputeNextTimerValue(541, Tier0Max, currentTimerValue: 0));
    }

    [Fact]
    public void ComputeNextTimerValue_TimerNegative_IsANoOp()
    {
        Assert.Equal(-5, DoublePetExpTimerGate.ComputeNextTimerValue(541, Tier0Max, currentTimerValue: -5));
    }
}
