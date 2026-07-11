using Fenrir.Application.Game.Domain.Pets;

namespace Fenrir.Application.Game.Tests.Pets;

/// <summary>
///     Covers <see cref="DoublePetExpTimerGate" /> against <c>PETSYSTEM::ReturnGrowPercent</c>
///     (<c>GameSystem_07_Pet.cpp:437-530</c>) as consumed by the once-per-120-ticks double-pet-EXP-timer
///     freeze gate (<c>S07_MyGame04.cpp:942-953</c>) -- the B8-pet-growth-depth contract's Part A.
/// </summary>
public class DoublePetExpTimerGateTests
{
    private const int Tier0Max = 40_000_000; // category 0 -- PetGrowthCaps.Values[0]
    private const int Tier1Max = 80_000_000; // category 1
    private const int Tier3Max = 320_000_000; // category 3

    [Fact]
    public void ResolveGrowthPercent_UnrecognizedItemId_ReturnsZero()
    {
        Assert.Equal(0f, DoublePetExpTimerGate.ResolveGrowthPercent(999_999, Tier0Max * 2));
    }

    [Fact]
    public void ResolveGrowthPercent_LegacyCategory0Id_MatchesFormula()
    {
        // item 541 -> category 0 -- 100% at tier max, 200% at twice tier max.
        Assert.Equal(100f, DoublePetExpTimerGate.ResolveGrowthPercent(541, Tier0Max));
        Assert.Equal(200f, DoublePetExpTimerGate.ResolveGrowthPercent(541, Tier0Max * 2));
    }

    [Fact]
    public void ResolveGrowthPercent_GiftEventCategory0Id_ResolvesSameAsLegacyId()
    {
        // 8202 is GIFT_EVENT category 0 -- re-verified live under both real build configurations.
        Assert.Equal(100f, DoublePetExpTimerGate.ResolveGrowthPercent(8202, Tier0Max));
    }

    [Fact]
    public void ResolveGrowthPercent_GiftEventCategory1Id_UsesCategory1TierMax()
    {
        // 8211 is GIFT_EVENT category 1 (tier max 80,000,000), not category 0.
        Assert.Equal(100f, DoublePetExpTimerGate.ResolveGrowthPercent(8211, Tier1Max));
        Assert.NotEqual(100f, DoublePetExpTimerGate.ResolveGrowthPercent(8211, Tier0Max));
    }

    [Fact]
    public void ResolveGrowthPercent_Category3IdIncludingHighLegacyIds_MatchesFormula()
    {
        // 2160/17057 are category-3 legacy ids per Table A.
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
        // Contract edge case: an unrecognized pet "is, from the gate's point of view, always below 200
        // percent" -- regardless of how large the raw growth counter is.
        Assert.False(DoublePetExpTimerGate.IsAtFreezeThreshold(999_999, int.MaxValue));
    }

    [Fact]
    public void IsAtFreezeThreshold_NoPetEquipped_TreatedSameAsUnrecognized()
    {
        // ItemId 0 (no pet equipped) is just another table miss -- never freezes.
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
        // The "unimpeded drain" edge case: no pet equipped must NOT freeze the countdown.
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
