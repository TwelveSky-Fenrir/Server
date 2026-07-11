using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Tests.Stats;

public class DrunkRageContributionTests
{
    [Fact]
    public void DrunkPotionIds_AreExactlyTheFiveRecognisedPotions()
    {
        Assert.Equal(5, StatCalculator.DrunkPotionIds.Count);
        Assert.Contains(878, (IEnumerable<int>)StatCalculator.DrunkPotionIds);
        Assert.Contains(879, (IEnumerable<int>)StatCalculator.DrunkPotionIds);
        Assert.Contains(880, (IEnumerable<int>)StatCalculator.DrunkPotionIds);
        Assert.Contains(881, (IEnumerable<int>)StatCalculator.DrunkPotionIds);
        Assert.Contains(882, (IEnumerable<int>)StatCalculator.DrunkPotionIds);
        Assert.DoesNotContain(0, (IEnumerable<int>)StatCalculator.DrunkPotionIds);
        Assert.DoesNotContain(877, (IEnumerable<int>)StatCalculator.DrunkPotionIds);
        Assert.DoesNotContain(883, (IEnumerable<int>)StatCalculator.DrunkPotionIds);
    }

    [Fact]
    public void DrunkEffectsById_HasAnEntryForEachRecognisedId_AndNoOthers()
    {
        Assert.Equal(5, StatCalculator.DrunkEffectsById.Count);
        foreach (var id in StatCalculator.DrunkPotionIds)
            Assert.True(StatCalculator.DrunkEffectsById.ContainsKey(id));
    }


    [Fact]
    public void DrunkEffect_878_ReducesMaxLife10PercentAndRaisesAttack10Percent()
    {
        var e = StatCalculator.DrunkEffectsById[878];
        Assert.Equal(90, e.MaxLifePercent);
        Assert.Equal(110, e.AttackPowerPercent);
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


    [Theory]
    [InlineData(1000, 100, 1000)]
    [InlineData(1000, 90, 900)]
    [InlineData(1000, 110, 1100)]
    [InlineData(1000, 80, 800)]
    [InlineData(1000, 200, 2000)]
    [InlineData(1000, 105, 1050)]
    [InlineData(1000, 130, 1300)]
    [InlineData(1000, 50, 500)]
    [InlineData(15, 90, 13)]
    [InlineData(105, 105, 110)]
    [InlineData(999, 50, 499)]
    [InlineData(7, 200, 14)]
    [InlineData(0, 130, 0)]
    public void ScaleByPercent_TruncatesTowardZero(int value, int percent, int expected)
    {
        Assert.Equal(expected, StatCalculator.ScaleByPercent(value, percent));
    }

    [Fact]
    public void ScaleByPercent_LargeValue_DoesNotOverflowIntermediateProduct()
    {
        Assert.Equal(1_800_000_000, StatCalculator.ScaleByPercent(2_000_000_000, 90));
    }


    [Fact]
    public void ApplyDrunkMaxLife_878_Reduces10Percent()
    {
        Assert.Equal(9000, StatCalculator.ApplyDrunkMaxLife(10000, new ZoneContext(DrunkStateId: 878)));
    }

    [Fact]
    public void ApplyDrunkMaxLife_NonMaxLifeId_LeavesValueUnchanged()
    {
        Assert.Equal(10000, StatCalculator.ApplyDrunkMaxLife(10000, new ZoneContext(DrunkStateId: 879)));
        Assert.Equal(10000, StatCalculator.ApplyDrunkMaxLife(10000, new ZoneContext(DrunkStateId: 0)));
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


    [Fact]
    public void SingleLiveDrunkId_OnlyThatIdsLegsApply_878()
    {
        var zone = new ZoneContext(DrunkStateId: 878);
        Assert.Equal(9000, StatCalculator.ApplyDrunkMaxLife(10000, zone));
        Assert.Equal(11000, StatCalculator.ApplyDrunkAttackPower(10000, zone));
        Assert.Equal(10000, StatCalculator.ApplyDrunkDefensePower(10000, zone));
        Assert.Equal(10000, StatCalculator.ApplyDrunkCritical(10000, zone));
        Assert.Equal(10000, StatCalculator.ApplyDrunkCriticalDefence(10000, zone));
        Assert.Equal(10000, StatCalculator.ApplyDrunkAttackSuccess(10000, zone));
        Assert.Equal(10000, StatCalculator.ApplyDrunkElementAttack(10000, zone));
        Assert.Equal(10000, StatCalculator.ApplyDrunkElementDefense(10000, zone));
    }


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


    [Fact]
    public void ApplyRageAttackMultiplier_IsDormant_ReturnsBaseAttackUnchanged()
    {
        Assert.Equal(10000, StatCalculator.ApplyRageAttackMultiplier(10000, new ZoneContext(RageGauge: 10)));
        Assert.Equal(10000, StatCalculator.ApplyRageAttackMultiplier(10000, new ZoneContext(RageGauge: 5)));
        Assert.Equal(10000, StatCalculator.ApplyRageAttackMultiplier(10000, default));
    }
}
