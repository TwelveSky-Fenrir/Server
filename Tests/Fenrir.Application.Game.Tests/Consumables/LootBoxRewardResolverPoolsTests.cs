using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Tests.Consumables;

/// <summary>
///     Deterministic coverage for the C10 additions to <see cref="LootBoxRewardResolver" />: the Mount-Box
///     banded-pool draw (<see cref="LootBoxRewardResolver.RollPools" /> /
///     <see cref="LootBoxRewardResolver.RollRareBandThenPools" />) and the shared pity counter step
///     (<see cref="LootBoxRewardResolver.PityStep" />). <see cref="ScriptedRandom" /> returns the draws in the
///     exact order the code requests them, so multi-draw shapes (rare band, then pool selection, then
///     within-pool uniform pick) are pinned rather than merely seeded.
/// </summary>
public class LootBoxRewardResolverPoolsTests
{
    // Mount Box's five pools over an inclusive 0..200 draw: ceilings 10/40/100/160/200.
    private static readonly ImmutableArray<LootBoxRewardResolver.RewardPool> MountPools =
    [
        new(10, [92286]),
        new(40, [1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326]),
        new(100, [611, 612, 652]),
        new(160, [506, 507, 508, 509, 578, 579]),
        new(200, [1166, 1118, 1103, 1222, 1145, 1237])
    ];

    [Fact]
    public void RollPools_DrawAtFirstPoolCeiling_PicksFirstPool()
    {
        // Pool-select draw 10 (<= ceiling 10) -> pool0; within-pool uniform draw 0 -> 92286.
        var id = LootBoxRewardResolver.RollPools(new ScriptedRandom(10, 0), MountPools);

        Assert.Equal(92286, id);
    }

    [Fact]
    public void RollPools_DrawJustAboveFirstCeiling_FallsToSecondPool()
    {
        // Pool-select draw 11 (> 10, <= 40) -> pool1; within-pool uniform draw 7 -> the eighth mount id.
        var id = LootBoxRewardResolver.RollPools(new ScriptedRandom(11, 7), MountPools);

        Assert.Equal(1326, id);
    }

    [Fact]
    public void RollPools_DrawAtMaxCeiling_PicksLastPool()
    {
        var id = LootBoxRewardResolver.RollPools(new ScriptedRandom(200, 0), MountPools);

        Assert.Equal(1166, id);
    }

    [Fact]
    public void RollPools_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            LootBoxRewardResolver.RollPools(new ScriptedRandom(0), ImmutableArray<LootBoxRewardResolver.RewardPool>.Empty));
    }

    [Fact]
    public void RollRareBandThenPools_RareBandHit_ReturnsBandRewardWithoutTouchingPools()
    {
        var rareBands = ImmutableArray.Create(new LootBoxRewardResolver.RewardBand(50, 635));

        // Rare draw 49 (< 50) -> 635. No pool draw is consumed (ScriptedRandom would throw if a second draw ran).
        var id = LootBoxRewardResolver.RollRareBandThenPools(new ScriptedRandom(49), rareBands, MountPools);

        Assert.Equal(635, id);
    }

    [Fact]
    public void RollRareBandThenPools_RareBandMiss_FallsThroughToPools()
    {
        var rareBands = ImmutableArray.Create(new LootBoxRewardResolver.RewardBand(50, 635));

        // Rare draw 50 (>= 50, miss); pool-select 45 (<= 100) -> pool2 {611,612,652}; uniform 2 -> 652.
        var id = LootBoxRewardResolver.RollRareBandThenPools(new ScriptedRandom(50, 45, 2), rareBands, MountPools);

        Assert.Equal(652, id);
    }

    [Theory]
    [InlineData(0, 100, false, 1)]
    [InlineData(98, 100, false, 99)]
    [InlineData(99, 100, true, 0)]
    [InlineData(100, 100, true, 0)]
    [InlineData(150, 100, true, 0)]
    [InlineData(198, 200, false, 199)]
    [InlineData(199, 200, true, 0)]
    public void PityStep_IncrementsUntilCeiling_ThenTriggersAndResets(int counter, int ceiling, bool expectTriggered,
        int expectNewCounter)
    {
        var result = LootBoxRewardResolver.PityStep(counter, ceiling);

        Assert.Equal(expectTriggered, result.Triggered);
        Assert.Equal(expectNewCounter, result.NewCounter);
    }

    /// <summary>Returns queued draws in request order; throws if the code draws more than were scripted.</summary>
    private sealed class ScriptedRandom(params int[] values) : Random
    {
        private int _index;

        public override int Next(int minValue, int maxValue)
        {
            if (_index >= values.Length)
                throw new InvalidOperationException("ScriptedRandom exhausted: the code drew more values than scripted.");

            return values[_index++];
        }
    }
}
