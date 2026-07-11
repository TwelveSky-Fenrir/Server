using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

/// <summary>
///     Guards the item-602 "Pet Box" reward table (workstream C10-petbox) in isolation from
///     <see cref="LootBoxCatalog" /> itself: this slice adds a new sibling data file rather than editing the
///     catalog directly (see the slice's wiring manifest), so these tests exercise
///     <see cref="PetBoxRewardTable" />'s own shape and rolls, not <c>LootBoxCatalog.Default.TryGetSpec(602)</c>
///     (which only starts returning this spec once the catalog's own <c>specs</c> list is edited in the serial
///     integration pass).
/// </summary>
public class PetBoxRewardTableTests
{
    [Fact]
    public void Spec_IsRareBandThenPools_ForBox602()
    {
        var spec = PetBoxRewardTable.Spec;

        Assert.Equal(602, spec.BoxId);
        Assert.Equal(BoxRewardKind.RareBandThenPools, spec.Kind);
        Assert.Equal(0, spec.RentalDays);
        // ImmutableArray<T> equality is reference-equality on the underlying array (not structural) -- confirm
        // the factory captured the same backing arrays rather than copying, via the struct's own == operator.
        Assert.True(spec.RareBands == PetBoxRewardTable.RareBands);
        Assert.True(spec.Pools == PetBoxRewardTable.Pools);
    }

    [Fact]
    public void RareBands_AreTwoBandsOf40EachSummingTo80()
    {
        Assert.Equal(2, PetBoxRewardTable.RareBands.Length);

        Assert.Equal(40, PetBoxRewardTable.RareBands[0].ThresholdPer10000);
        Assert.Equal(1012, PetBoxRewardTable.RareBands[0].RewardItemId);

        Assert.Equal(40, PetBoxRewardTable.RareBands[1].ThresholdPer10000);
        Assert.Equal(1016, PetBoxRewardTable.RareBands[1].RewardItemId);
    }

    [Fact]
    public void Pools_AreFiveBands_WithTheContractsExactCeilingsAndItemCounts()
    {
        var pools = PetBoxRewardTable.Pools;
        Assert.Equal(5, pools.Length);

        Assert.Equal(20, pools[0].ThresholdCeilingInclusive);
        Assert.Single(pools[0].Ids);
        Assert.Equal(1178, pools[0].Ids[0]);

        Assert.Equal(60, pools[1].ThresholdCeilingInclusive);
        Assert.Equal(4, pools[1].Ids.Length);
        Assert.True(pools[1].Ids.Contains(1002));
        Assert.True(pools[1].Ids.Contains(1003));
        Assert.True(pools[1].Ids.Contains(1004));
        Assert.True(pools[1].Ids.Contains(1005));

        Assert.Equal(120, pools[2].ThresholdCeilingInclusive);
        Assert.Equal(3, pools[2].Ids.Length);
        Assert.True(pools[2].Ids.Contains(1190));
        Assert.True(pools[2].Ids.Contains(1491));
        Assert.True(pools[2].Ids.Contains(1492));

        Assert.Equal(180, pools[3].ThresholdCeilingInclusive);
        Assert.Equal(6, pools[3].Ids.Length);
        int[] band4Expected = [506, 507, 508, 509, 578, 579];
        Assert.Equal(band4Expected, pools[3].Ids.ToArray());

        Assert.Equal(199, pools[4].ThresholdCeilingInclusive);
        Assert.Equal(6, pools[4].Ids.Length);
        int[] band5Expected = [1103, 1118, 1145, 1166, 1222, 1237];
        Assert.Equal(band5Expected, pools[4].Ids.ToArray());
    }

    [Fact]
    public void PoolWidths_MatchTheContractsBandWidthTable_21_40_60_60_19()
    {
        var pools = PetBoxRewardTable.Pools;

        // Width = this ceiling minus the previous one (the first pool's width is its ceiling + 1, since the
        // draw range starts at 0 inclusive).
        Assert.Equal(21, pools[0].ThresholdCeilingInclusive + 1);
        Assert.Equal(40, pools[1].ThresholdCeilingInclusive - pools[0].ThresholdCeilingInclusive);
        Assert.Equal(60, pools[2].ThresholdCeilingInclusive - pools[1].ThresholdCeilingInclusive);
        Assert.Equal(60, pools[3].ThresholdCeilingInclusive - pools[2].ThresholdCeilingInclusive);
        Assert.Equal(19, pools[4].ThresholdCeilingInclusive - pools[3].ThresholdCeilingInclusive);

        // Full range is 0-199 inclusive (200 values), matching the contract's own "0-199 range" wording.
        Assert.Equal(199, pools[^1].ThresholdCeilingInclusive);
    }

    [Fact]
    public void RollRareBandThenPools_BottomHalfDraw_ReturnsFirstSpecialId_WithoutTouchingPools()
    {
        // Rare draw 0 (< 40) -> 1012. ScriptedRandom would throw if a pool draw were also consumed.
        var id = LootBoxRewardResolver.RollRareBandThenPools(new ScriptedRandom(0), PetBoxRewardTable.RareBands,
            PetBoxRewardTable.Pools);

        Assert.Equal(1012, id);
    }

    [Fact]
    public void RollRareBandThenPools_TopHalfDraw_ReturnsSecondSpecialId()
    {
        // Rare draw 40 (>= 40, < 80) -> 1016.
        var id = LootBoxRewardResolver.RollRareBandThenPools(new ScriptedRandom(40), PetBoxRewardTable.RareBands,
            PetBoxRewardTable.Pools);

        Assert.Equal(1016, id);
    }

    [Fact]
    public void RollRareBandThenPools_JustAboveSpecialTier_FallsThroughToFirstFallbackBand()
    {
        // Rare draw 80 (miss, exactly the 0.80% boundary); pool-select draw 0 (<= 20) -> band 1 -> 1178
        // (single-item pool, uniform draw 0 is the only legal index).
        var id = LootBoxRewardResolver.RollRareBandThenPools(new ScriptedRandom(80, 0, 0),
            PetBoxRewardTable.RareBands, PetBoxRewardTable.Pools);

        Assert.Equal(1178, id);
    }

    [Fact]
    public void RollRareBandThenPools_MaxFallbackDraw_ReturnsFromLastBand()
    {
        // Rare draw 9999 (miss); pool-select draw 199 (<= 199, the max) -> band 5; uniform 5 -> last id in that pool.
        var id = LootBoxRewardResolver.RollRareBandThenPools(new ScriptedRandom(9999, 199, 5),
            PetBoxRewardTable.RareBands, PetBoxRewardTable.Pools);

        Assert.Equal(1237, id);
    }

    [Fact]
    public void RollRareBandThenPools_MidFallbackDraw_LandsInThirdBand()
    {
        // Rare draw 9999 (miss); pool-select draw 61 (> 60, <= 120) -> band 3 {1190,1491,1492}; uniform 1 -> 1491.
        var id = LootBoxRewardResolver.RollRareBandThenPools(new ScriptedRandom(9999, 61, 1),
            PetBoxRewardTable.RareBands, PetBoxRewardTable.Pools);

        Assert.Equal(1491, id);
    }

    /// <summary>Returns queued draws in request order; throws if the code draws more than were scripted.</summary>
    private sealed class ScriptedRandom(params int[] values) : Random
    {
        private int _index;

        public override int Next(int minValue, int maxValue)
        {
            if (_index >= values.Length)
                throw new InvalidOperationException(
                    "ScriptedRandom exhausted: the code drew more values than scripted.");

            return values[_index++];
        }
    }
}
