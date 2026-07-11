using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Workstream B7 -- drunk-potion stat wrappers (ids 878-882) and the (dormant) rage-buff attack multiplier.
///     These pin the id/value tables and the pure, int-truncation-exact contribution functions on
///     <see cref="StatCalculator" />'s drunk/rage surface directly. The call-site insertion into the individual
///     stat getters is the integration pass's job (see the workstream wiringManifest), so these tests exercise
///     the contribution primitives rather than the end-to-end <c>ComputeBaseStats</c>/<c>ComputeEffectiveStats</c>
///     pipeline, which does not yet consume them.
/// </summary>
public class DrunkRageContributionTests
{
    // ---- Recognised id set ----

    [Fact]
    public void DrunkPotionIds_AreExactlyTheFiveRecognisedPotions()
    {
        Assert.Equal(5, StatCalculator.DrunkPotionIds.Count);
        Assert.Contains(878, StatCalculator.DrunkPotionIds);
        Assert.Contains(879, StatCalculator.DrunkPotionIds);
        Assert.Contains(880, StatCalculator.DrunkPotionIds);
        Assert.Contains(881, StatCalculator.DrunkPotionIds);
        Assert.Contains(882, StatCalculator.DrunkPotionIds);
        Assert.DoesNotContain(0, StatCalculator.DrunkPotionIds);
        Assert.DoesNotContain(877, StatCalculator.DrunkPotionIds);
        Assert.DoesNotContain(883, StatCalculator.DrunkPotionIds);
    }

    [Fact]
    public void DrunkEffectsById_HasAnEntryForEachRecognisedId_AndNoOthers()
    {
        Assert.Equal(5, StatCalculator.DrunkEffectsById.Count);
        foreach (var id in StatCalculator.DrunkPotionIds)
            Assert.True(StatCalculator.DrunkEffectsById.ContainsKey(id));
    }

    // ---- Per-id factor matrix (the cited magnitudes) ----

    [Fact]
    public void DrunkEffect_878_ReducesMaxLife10PercentAndRaisesAttack10Percent()
    {
        var e = StatCalculator.DrunkEffectsById[878];
        Assert.Equal(90, e.MaxLifePercent);
        Assert.Equal(110, e.AttackPowerPercent);
        // Untouched stats stay neutral (100).
        Assert.Equal(100, e.DefensePowerPercent);
        Assert.Equal(100, e.CriticalPercent);
        Assert.Equal(100, e.CriticalDefencePercent);
        Assert.Equal(100, e.AttackSuccessPercent);
        Assert.Equal(100, e.ElementAttackPercent);
        Assert.Equal(100, e.ElementDefensePercent);
    }

    [Fact]
    public void DrunkEffect_879_ReducesAttack20PercentAndDoublesDefense()
    {
        var e = StatCalculator.DrunkEffectsById[879];
        Assert.Equal(80, e.AttackPowerPercent);
        Assert.Equal(200, e.DefensePowerPercent);
        Assert.Equal(100, e.MaxLifePercent);
        Assert.Equal(100, e.CriticalPercent);
        Assert.Equal(100, e.CriticalDefencePercent);
        Assert.Equal(100, e.AttackSuccessPercent);
        Assert.Equal(100, e.ElementAttackPercent);
        Assert.Equal(100, e.ElementDefensePercent);
    }

    [Fact]
    public void DrunkEffect_880_ReducesCriticalDefence10PercentAndRaisesCritical5Percent()
    {
        var e = StatCalculator.DrunkEffectsById[880];
        Assert.Equal(90, e.CriticalDefencePercent);
        Assert.Equal(105, e.CriticalPercent);
        Assert.Equal(100, e.MaxLifePercent);
        Assert.Equal(100, e.AttackPowerPercent);
        Assert.Equal(100, e.DefensePowerPercent);
        Assert.Equal(100, e.AttackSuccessPercent);
        Assert.Equal(100, e.ElementAttackPercent);
        Assert.Equal(100, e.ElementDefensePercent);
    }

    [Fact]
    public void DrunkEffect_881_ReducesAttackSuccess20PercentOnly()
    {
        var e = StatCalculator.DrunkEffectsById[881];
        Assert.Equal(80, e.AttackSuccessPercent);
        // 881 is one-sided within combat-stat scope: no offsetting benefit.
        Assert.Equal(100, e.MaxLifePercent);
        Assert.Equal(100, e.AttackPowerPercent);
        Assert.Equal(100, e.DefensePowerPercent);
        Assert.Equal(100, e.CriticalPercent);
        Assert.Equal(100, e.CriticalDefencePercent);
        Assert.Equal(100, e.ElementAttackPercent);
        Assert.Equal(100, e.ElementDefensePercent);
    }

    [Fact]
    public void DrunkEffect_882_RaisesElementAttack30PercentAndHalvesElementDefense()
    {
        var e = StatCalculator.DrunkEffectsById[882];
        Assert.Equal(130, e.ElementAttackPercent);
        Assert.Equal(50, e.ElementDefensePercent);
        Assert.Equal(100, e.MaxLifePercent);
        Assert.Equal(100, e.AttackPowerPercent);
        Assert.Equal(100, e.DefensePowerPercent);
        Assert.Equal(100, e.CriticalPercent);
        Assert.Equal(100, e.CriticalDefencePercent);
        Assert.Equal(100, e.AttackSuccessPercent);
    }

    // ---- ScaleByPercent: truncated-toward-zero integer of value * (percent/100) ----

    [Theory]
    [InlineData(1000, 100, 1000)] // unchanged fast-path
    [InlineData(1000, 90, 900)] // -10%
    [InlineData(1000, 110, 1100)] // +10%
    [InlineData(1000, 80, 800)] // -20%
    [InlineData(1000, 200, 2000)] // doubled
    [InlineData(1000, 105, 1050)] // +5%
    [InlineData(1000, 130, 1300)] // +30%
    [InlineData(1000, 50, 500)] // -50%
    [InlineData(15, 90, 13)] // 13.5 -> 13 (truncates toward zero)
    [InlineData(105, 105, 110)] // 110.25 -> 110
    [InlineData(999, 50, 499)] // 499.5 -> 499
    [InlineData(7, 200, 14)]
    [InlineData(0, 130, 0)] // zero stays zero
    public void ScaleByPercent_TruncatesTowardZero(int value, int percent, int expected)
    {
        Assert.Equal(expected, StatCalculator.ScaleByPercent(value, percent));
    }

    [Fact]
    public void ScaleByPercent_LargeValue_DoesNotOverflowIntermediateProduct()
    {
        // value * percent would overflow a 32-bit product (2_000_000_000 * 130 > int.MaxValue) if not widened.
        Assert.Equal(1_800_000_000, StatCalculator.ScaleByPercent(2_000_000_000, 90));
    }

    // ---- CACHED legs ----

    [Fact]
    public void ApplyDrunkMaxLife_878_Reduces10Percent()
    {
        Assert.Equal(9000, StatCalculator.ApplyDrunkMaxLife(10000, new ZoneContext(DrunkStateId: 878)));
    }

    [Fact]
    public void ApplyDrunkMaxLife_NonMaxLifeId_LeavesValueUnchanged()
    {
        // 879 is a live drunk id but has no max-life leg -> no change.
        Assert.Equal(10000, StatCalculator.ApplyDrunkMaxLife(10000, new ZoneContext(DrunkStateId: 879)));
        // Not drunk (0) -> no change.
        Assert.Equal(10000, StatCalculator.ApplyDrunkMaxLife(10000, new ZoneContext(DrunkStateId: 0)));
        // Unrecognised id -> no change.
        Assert.Equal(10000, StatCalculator.ApplyDrunkMaxLife(10000, new ZoneContext(DrunkStateId: 12345)));
    }

    [Fact]
    public void ApplyDrunkCriticalDefence_880_Reduces10Percent()
    {
        Assert.Equal(900, StatCalculator.ApplyDrunkCriticalDefence(1000, new ZoneContext(DrunkStateId: 880)));
    }

    [Fact]
    public void ApplyDrunkCriticalDefence_NonMatchingId_LeavesValueUnchanged()
    {
        Assert.Equal(1000, StatCalculator.ApplyDrunkCriticalDefence(1000, new ZoneContext(DrunkStateId: 878)));
        Assert.Equal(1000, StatCalculator.ApplyDrunkCriticalDefence(1000, new ZoneContext(DrunkStateId: 0)));
    }

    // ---- EFFECTIVE legs ----

    [Fact]
    public void ApplyDrunkAttackPower_878_Raises10Percent_879_Reduces20Percent()
    {
        Assert.Equal(11000, StatCalculator.ApplyDrunkAttackPower(10000, new ZoneContext(DrunkStateId: 878)));
        Assert.Equal(8000, StatCalculator.ApplyDrunkAttackPower(10000, new ZoneContext(DrunkStateId: 879)));
    }

    [Fact]
    public void ApplyDrunkAttackPower_NonAttackId_LeavesValueUnchanged()
    {
        Assert.Equal(10000, StatCalculator.ApplyDrunkAttackPower(10000, new ZoneContext(DrunkStateId: 880)));
        Assert.Equal(10000, StatCalculator.ApplyDrunkAttackPower(10000, new ZoneContext(DrunkStateId: 0)));
    }

    [Fact]
    public void ApplyDrunkDefensePower_879_Doubles()
    {
        Assert.Equal(20000, StatCalculator.ApplyDrunkDefensePower(10000, new ZoneContext(DrunkStateId: 879)));
        Assert.Equal(10000, StatCalculator.ApplyDrunkDefensePower(10000, new ZoneContext(DrunkStateId: 878)));
    }

    [Fact]
    public void ApplyDrunkCritical_880_Raises5Percent()
    {
        Assert.Equal(2100, StatCalculator.ApplyDrunkCritical(2000, new ZoneContext(DrunkStateId: 880)));
        Assert.Equal(2000, StatCalculator.ApplyDrunkCritical(2000, new ZoneContext(DrunkStateId: 881)));
    }

    [Fact]
    public void ApplyDrunkAttackSuccess_881_Reduces20Percent()
    {
        Assert.Equal(8000, StatCalculator.ApplyDrunkAttackSuccess(10000, new ZoneContext(DrunkStateId: 881)));
        Assert.Equal(10000, StatCalculator.ApplyDrunkAttackSuccess(10000, new ZoneContext(DrunkStateId: 878)));
    }

    [Fact]
    public void ApplyDrunkElementAttack_882_Raises30Percent()
    {
        Assert.Equal(1300, StatCalculator.ApplyDrunkElementAttack(1000, new ZoneContext(DrunkStateId: 882)));
        Assert.Equal(1000, StatCalculator.ApplyDrunkElementAttack(1000, new ZoneContext(DrunkStateId: 881)));
    }

    [Fact]
    public void ApplyDrunkElementDefense_882_Halves()
    {
        Assert.Equal(500, StatCalculator.ApplyDrunkElementDefense(1000, new ZoneContext(DrunkStateId: 882)));
        Assert.Equal(1000, StatCalculator.ApplyDrunkElementDefense(1000, new ZoneContext(DrunkStateId: 881)));
    }

    // ---- Only one drunk id is ever live at a time (single selected slot) ----

    [Fact]
    public void SingleLiveDrunkId_OnlyThatIdsLegsApply_878()
    {
        var zone = new ZoneContext(DrunkStateId: 878);
        // 878 touches max-life and attack only.
        Assert.Equal(9000, StatCalculator.ApplyDrunkMaxLife(10000, zone));
        Assert.Equal(11000, StatCalculator.ApplyDrunkAttackPower(10000, zone));
        // Everything else the wrappers cover is untouched.
        Assert.Equal(10000, StatCalculator.ApplyDrunkDefensePower(10000, zone));
        Assert.Equal(10000, StatCalculator.ApplyDrunkCritical(10000, zone));
        Assert.Equal(10000, StatCalculator.ApplyDrunkCriticalDefence(10000, zone));
        Assert.Equal(10000, StatCalculator.ApplyDrunkAttackSuccess(10000, zone));
        Assert.Equal(10000, StatCalculator.ApplyDrunkElementAttack(10000, zone));
        Assert.Equal(10000, StatCalculator.ApplyDrunkElementDefense(10000, zone));
    }

    // ---- ResolveDrunkEffect ----

    [Fact]
    public void ResolveDrunkEffect_RecognisedId_ReturnsEffect()
    {
        var e = StatCalculator.ResolveDrunkEffect(882);
        Assert.NotNull(e);
        Assert.Equal(130, e!.Value.ElementAttackPercent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(877)]
    [InlineData(883)]
    [InlineData(-5)]
    public void ResolveDrunkEffect_UnrecognisedId_ReturnsNull(int id)
    {
        Assert.Null(StatCalculator.ResolveDrunkEffect(id));
    }

    // ---- Rage-buff table + resolver ----

    [Theory]
    [InlineData(1, 105)]
    [InlineData(2, 107)]
    [InlineData(3, 109)]
    [InlineData(4, 111)]
    [InlineData(5, 114)]
    [InlineData(6, 117)]
    [InlineData(7, 120)]
    [InlineData(8, 125)]
    [InlineData(9, 130)]
    [InlineData(10, 140)]
    public void ResolveRageBuffPercent_Gauge1To10_MapsToCitedRate(int gauge, int expectedPercent)
    {
        Assert.Equal(expectedPercent, StatCalculator.ResolveRageBuffPercent(gauge));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(100)]
    [InlineData(-1)]
    public void ResolveRageBuffPercent_GaugeOutsideTable_IsNeutral100(int gauge)
    {
        Assert.Equal(100, StatCalculator.ResolveRageBuffPercent(gauge));
    }

    [Fact]
    public void RageBuffPercentByGauge_HasExactlyTenEntries()
    {
        Assert.Equal(10, StatCalculator.RageBuffPercentByGauge.Count);
    }

    // ---- Rage multiplier is dormant in the shipped build ----

    [Fact]
    public void ApplyRageAttackMultiplier_IsDormant_ReturnsBaseAttackUnchanged()
    {
        // Even with a populated gauge, the multiplier never fires: there is no rage-buff-state gate to satisfy,
        // and the shipped legacy build never enables it (contract dead-vestige edge case).
        Assert.Equal(10000, StatCalculator.ApplyRageAttackMultiplier(10000, new ZoneContext(RageGauge: 10)));
        Assert.Equal(10000, StatCalculator.ApplyRageAttackMultiplier(10000, new ZoneContext(RageGauge: 5)));
        Assert.Equal(10000, StatCalculator.ApplyRageAttackMultiplier(10000, default));
    }
}
