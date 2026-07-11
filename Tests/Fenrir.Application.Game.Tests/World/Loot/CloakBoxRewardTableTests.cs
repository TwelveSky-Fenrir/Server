using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

/// <summary>
///     Guards the item-2249 "Cloak Box" reward + pity table (workstream C10-cloakbox2249) in isolation from
///     <see cref="LootBoxCatalog" /> itself: this slice adds a new sibling data file rather than editing the
///     catalog directly (see the slice's wiring manifest), so these tests exercise
///     <see cref="CloakBoxRewardTable" />'s own shape and rolls, not <c>LootBoxCatalog.Default.TryGetSpec(2249)</c>
///     (which only starts returning a spec once the catalog's own <c>specs</c> list is edited in the serial
///     integration pass, together with the pity-threading edits to <c>LootBoxOpenResolver</c>/
///     <c>LootBoxUseItemHandler</c>).
/// </summary>
public class CloakBoxRewardTableTests
{
    [Fact]
    public void Spec_IsRareBandThenPools_ForBox2249_PostPityShapeOnly()
    {
        var spec = CloakBoxRewardTable.Spec;

        Assert.Equal(2249, spec.BoxId);
        Assert.Equal(BoxRewardKind.RareBandThenPools, spec.Kind);
        Assert.Equal(0, spec.RentalDays);
        Assert.Equal(CloakBoxRewardTable.RareBands, spec.RareBands);
        Assert.Equal(CloakBoxRewardTable.Pools, spec.Pools);
    }

    [Fact]
    public void RareBand_IsSingleBand_60In10000_ToWarlordCape1403()
    {
        Assert.Single(CloakBoxRewardTable.RareBands);
        Assert.Equal(60, CloakBoxRewardTable.RareBands[0].ThresholdPer10000);
        Assert.Equal(1403, CloakBoxRewardTable.RareBands[0].RewardItemId);
    }

    [Fact]
    public void Pools_AreTwoBands_PotionThenScrollCharm_WithTheContractsExactIds()
    {
        var pools = CloakBoxRewardTable.Pools;
        Assert.Equal(2, pools.Length);

        Assert.Equal(100, pools[0].ThresholdCeilingInclusive);
        int[] potionExpected = [506, 507, 508, 509, 578, 579];
        Assert.Equal(potionExpected, pools[0].Ids.ToArray());

        Assert.Equal(199, pools[1].ThresholdCeilingInclusive);
        int[] charmExpected = [1166, 1118, 1103, 1222, 1145, 1237];
        Assert.Equal(charmExpected, pools[1].Ids.ToArray());
    }

    [Fact]
    public void PoolWidths_Are101And99_Spanning0To199Inclusive_MatchingContractsExactWording()
    {
        var pools = CloakBoxRewardTable.Pools;

        // Width = this ceiling minus the previous one (the first pool's width is its ceiling + 1, since the
        // draw range starts at 0 inclusive).
        Assert.Equal(101, pools[0].ThresholdCeilingInclusive + 1);
        Assert.Equal(99, pools[1].ThresholdCeilingInclusive - pools[0].ThresholdCeilingInclusive);

        // Full range is 0-199 inclusive (200 values), matching the contract's own "0-199 range" wording.
        Assert.Equal(199, pools[^1].ThresholdCeilingInclusive);
    }

    [Fact]
    public void PityCeiling_Is100_AndGuaranteedReward_Is1401UltimateCloak()
    {
        Assert.Equal(100, CloakBoxRewardTable.PityCeiling);
        Assert.Equal(1401, CloakBoxRewardTable.GuaranteedRewardItemId);
        Assert.Equal(2249, CloakBoxRewardTable.BoxId);
    }

    [Fact]
    public void Roll_PityCounterAt99_TriggersGuaranteedReward_ResetsToZero_AndConsumesZeroRandomDraws()
    {
        // No scripted values at all: ScriptedRandom throws if Next is ever called, proving the pity branch
        // never touches random.
        var result = CloakBoxRewardTable.Roll(99, new ScriptedRandom());

        Assert.Equal(1401, result.RewardItemId);
        Assert.Equal(0, result.NewPityCounter);
        Assert.True(result.WasPityTriggered);
    }

    [Fact]
    public void Roll_PityCounterWellPastCeiling_StillTriggers_ReachedNotOnlyEqualled()
    {
        var result = CloakBoxRewardTable.Roll(150, new ScriptedRandom());

        Assert.Equal(1401, result.RewardItemId);
        Assert.Equal(0, result.NewPityCounter);
        Assert.True(result.WasPityTriggered);
    }

    [Fact]
    public void Roll_LegacyPreMigrationCounterMinusOne_FirstOpenDoesNotTrigger_NeedsOneExtraOpen()
    {
        // The -1 schema default (never applied to a freshly created character) increments to 0 on its first
        // open -- still below the 100 ceiling, so that account's very first pity cycle needs 101 opens.
        var result = CloakBoxRewardTable.Roll(-1, new ScriptedRandom(9999, 199, 5));

        Assert.False(result.WasPityTriggered);
        Assert.Equal(0, result.NewPityCounter);
        Assert.Equal(1237, result.RewardItemId);
    }

    [Fact]
    public void Roll_PityBelowCeiling_SpecialRollHits_ReturnsWarlordCape_IncrementsCounter_AndNeverTouchesPools()
    {
        // Special draw 0 (< 60) hits. Only one scripted value: ScriptedRandom throws if a pool draw were also
        // consumed, proving a special-tier hit short-circuits before the fallback pools.
        var result = CloakBoxRewardTable.Roll(10, new ScriptedRandom(0));

        Assert.Equal(1403, result.RewardItemId);
        Assert.Equal(11, result.NewPityCounter);
        Assert.False(result.WasPityTriggered);
    }

    [Fact]
    public void Roll_PityBelowCeiling_SpecialRollMisses_PotionPoolLowBoundary_Draw0SelectsFirstPotionId()
    {
        // Special draw 60 (miss, the exact 0.6% boundary); pool-select draw 0 (<= 100) -> potion pool;
        // within-pool draw 0 -> first id.
        var result = CloakBoxRewardTable.Roll(10, new ScriptedRandom(60, 0, 0));

        Assert.Equal(506, result.RewardItemId);
        Assert.Equal(11, result.NewPityCounter);
        Assert.False(result.WasPityTriggered);
    }

    [Fact]
    public void Roll_PityBelowCeiling_SpecialRollMisses_PotionPoolHighBoundary_Draw100SelectsLastPotionId()
    {
        // Pool-select draw 100 is still <= 100 (inclusive ceiling) -> potion pool, not the charm pool.
        var result = CloakBoxRewardTable.Roll(10, new ScriptedRandom(9999, 100, 5));

        Assert.Equal(579, result.RewardItemId);
        Assert.Equal(11, result.NewPityCounter);
    }

    [Fact]
    public void Roll_PityBelowCeiling_SpecialRollMisses_CharmPoolLowBoundary_Draw101SelectsFirstCharmId()
    {
        // Pool-select draw 101 is the first value past the potion pool's ceiling -> charm pool.
        var result = CloakBoxRewardTable.Roll(10, new ScriptedRandom(9999, 101, 0));

        Assert.Equal(1166, result.RewardItemId);
        Assert.Equal(11, result.NewPityCounter);
    }

    [Fact]
    public void Roll_PityBelowCeiling_SpecialRollMisses_CharmPoolHighBoundary_Draw199SelectsLastCharmId()
    {
        var result = CloakBoxRewardTable.Roll(10, new ScriptedRandom(9999, 199, 5));

        Assert.Equal(1237, result.RewardItemId);
        Assert.Equal(11, result.NewPityCounter);
    }

    [Fact]
    public void RollRareBandThenPools_DirectlyAgainstTheTable_MatchesTheSameShapeMountAndPetBoxesUse()
    {
        // Sanity cross-check: CloakBoxRewardTable.Spec's own roll shape is the identical
        // RollRareBandThenPools/RollPools composition the 601/602 sibling boxes already use, exercised here
        // independent of the pity gate.
        var id = LootBoxRewardResolver.RollRareBandThenPools(new ScriptedRandom(9999, 0, 3),
            CloakBoxRewardTable.RareBands, CloakBoxRewardTable.Pools);

        Assert.Equal(509, id); // potion pool, index 3 -> 509
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
