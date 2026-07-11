using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Tests.Stats;

public class StellarCoreStatContributionTests
{
    private static readonly int[] Tier1Ids =
        [76527, 76528, 76529, 76530, 76531, 76532, 76533, 76534, 76535, 76536, 76537, 76538, 76539, 76540];

    private static readonly int[] Tier2Ids =
        [93500, 93501, 93502, 93503, 93504, 93505, 93506, 93507, 93508, 93509, 93510, 93511, 93512, 93513];

    private static readonly int[] InvalidIds =
        [-1, 1, 100, 8001, 76526, 76541, 93499, 93514, 100000];


    private static readonly (int Id, int Bonus)[] DamageDefenseVectors =
    [
        (76527, 50), (76528, 150), (76529, 200), (76530, 300), (76531, 350), (76532, 400), (76533, 450),
        (76534, 500), (76535, 550), (76536, 600), (76537, 650), (76538, 700), (76539, 750), (76540, 900),
        (93500, 125), (93501, 375), (93502, 500), (93503, 750), (93504, 875), (93505, 1000), (93506, 1125),
        (93507, 1250), (93508, 1375), (93509, 1500), (93510, 1625), (93511, 1750), (93512, 1875), (93513, 2250)
    ];


    private static readonly (int Id, int Bonus)[] ElementVectors =
    [
        (76527, 5), (76528, 10), (76529, 15), (76530, 20), (76531, 25), (76532, 30), (76533, 35),
        (76534, 40), (76535, 45), (76536, 50), (76537, 55), (76538, 60), (76539, 65), (76540, 70),
        (93500, 125), (93501, 250), (93502, 375), (93503, 500), (93504, 625), (93505, 750), (93506, 875),
        (93507, 1000), (93508, 1125), (93509, 1250), (93510, 1375), (93511, 1500), (93512, 1625), (93513, 1750)
    ];


    private static readonly (int Id, int Bonus)[] CriticalDefenceVectors =
    [
        (76530, 1), (76531, 1), (76532, 2), (76533, 2), (76534, 3), (76535, 3),
        (76536, 4), (76537, 4), (76538, 5), (76539, 5), (76540, 6),
        (93503, 1), (93504, 1), (93505, 2), (93506, 2), (93507, 3), (93508, 3),
        (93509, 4), (93510, 4), (93511, 6), (93512, 8), (93513, 10)
    ];


    private static readonly (int Id, int Bonus)[] MaxLifeVectors =
    [
        (93500, 250), (93501, 500), (93502, 750), (93503, 1000), (93504, 1250), (93505, 1500), (93506, 1750),
        (93507, 2000), (93508, 2250), (93509, 2500), (93510, 2750), (93511, 3000), (93512, 3250), (93513, 3500)
    ];

    private static CosmeticContext Core(int coreId)
    {
        return new CosmeticContext(StellarCoreNumber: coreId);
    }

    [Fact]
    public void AttackPower_EveryValidCore_MatchesTranscribedTable()
    {
        foreach (var (id, bonus) in DamageDefenseVectors)
            Assert.Equal(bonus, StatCalculator.StellarCoreAttackPowerContribution(Core(id)));
    }

    [Fact]
    public void DefensePower_IsByteForByteIdenticalToAttackPower_ForEveryValidCore()
    {
        foreach (var (id, bonus) in DamageDefenseVectors)
        {
            Assert.Equal(bonus, StatCalculator.StellarCoreDefensePowerContribution(Core(id)));
            Assert.Equal(
                StatCalculator.StellarCoreAttackPowerContribution(Core(id)),
                StatCalculator.StellarCoreDefensePowerContribution(Core(id)));
        }
    }

    [Fact]
    public void AttackAndDefense_TopOfBandJumps_ArePreserved_NotSmoothed()
    {
        Assert.Equal(750, StatCalculator.StellarCoreAttackPowerContribution(Core(76539)));
        Assert.Equal(900, StatCalculator.StellarCoreAttackPowerContribution(Core(76540)));
        Assert.Equal(1875, StatCalculator.StellarCoreDefensePowerContribution(Core(93512)));
        Assert.Equal(2250, StatCalculator.StellarCoreDefensePowerContribution(Core(93513)));
    }

    [Fact]
    public void ElementAttack_EveryValidCore_MatchesTranscribedTable()
    {
        foreach (var (id, bonus) in ElementVectors)
            Assert.Equal(bonus, StatCalculator.StellarCoreElementAttackContribution(Core(id)));
    }

    [Fact]
    public void ElementDefense_IsByteForByteIdenticalToElementAttack_ForEveryValidCore()
    {
        foreach (var (id, bonus) in ElementVectors)
        {
            Assert.Equal(bonus, StatCalculator.StellarCoreElementDefenseContribution(Core(id)));
            Assert.Equal(
                StatCalculator.StellarCoreElementAttackContribution(Core(id)),
                StatCalculator.StellarCoreElementDefenseContribution(Core(id)));
        }
    }

    [Fact]
    public void Element_StepsAreClean_NoTopOfBandJump()
    {
        Assert.Equal(70, StatCalculator.StellarCoreElementAttackContribution(Core(76540)));
        Assert.Equal(1750, StatCalculator.StellarCoreElementAttackContribution(Core(93513)));
    }

    [Fact]
    public void CriticalDefence_EveryEntryInNarrowTable_MatchesTranscribedValue()
    {
        foreach (var (id, bonus) in CriticalDefenceVectors)
            Assert.Equal(bonus, StatCalculator.StellarCoreCriticalDefenceContribution(Core(id)));
    }

    [Fact]
    public void CriticalDefence_OmitsThreeLowestOfEachBand_EvenThoughTheyAreValidCores()
    {
        foreach (var omitted in (int[])[76527, 76528, 76529, 93500, 93501, 93502])
        {
            Assert.Equal(0, StatCalculator.StellarCoreCriticalDefenceContribution(Core(omitted)));
            Assert.True(StatCalculator.StellarCoreAttackPowerContribution(Core(omitted)) > 0);
        }
    }

    [Fact]
    public void CriticalDefence_Tier2PairsPattern_BreaksTo6_8_10AtTheTop()
    {
        Assert.Equal(4, StatCalculator.StellarCoreCriticalDefenceContribution(Core(93510)));
        Assert.Equal(6, StatCalculator.StellarCoreCriticalDefenceContribution(Core(93511)));
        Assert.Equal(8, StatCalculator.StellarCoreCriticalDefenceContribution(Core(93512)));
        Assert.Equal(10, StatCalculator.StellarCoreCriticalDefenceContribution(Core(93513)));
    }

    [Fact]
    public void MaxLife_EveryTier2Core_MatchesTranscribedTable()
    {
        foreach (var (id, bonus) in MaxLifeVectors)
            Assert.Equal(bonus, StatCalculator.StellarCoreMaxLifeContribution(Core(id)));
    }

    [Fact]
    public void MaxLife_EveryTier1Core_GrantsZero_EvenThoughItGrantsEveryOtherStat()
    {
        foreach (var tier1 in Tier1Ids)
        {
            Assert.Equal(0, StatCalculator.StellarCoreMaxLifeContribution(Core(tier1)));
            Assert.True(StatCalculator.StellarCoreAttackPowerContribution(Core(tier1)) > 0);
        }
    }


    [Fact]
    public void NoCoreActive_ContributesZeroToEveryStat()
    {
        var none = Core(0);
        Assert.Equal(0, StatCalculator.StellarCoreAttackPowerContribution(none));
        Assert.Equal(0, StatCalculator.StellarCoreDefensePowerContribution(none));
        Assert.Equal(0, StatCalculator.StellarCoreCriticalDefenceContribution(none));
        Assert.Equal(0, StatCalculator.StellarCoreElementAttackContribution(none));
        Assert.Equal(0, StatCalculator.StellarCoreElementDefenseContribution(none));
        Assert.Equal(0, StatCalculator.StellarCoreMaxLifeContribution(none));
    }

    [Fact]
    public void DefaultCosmeticContext_ContributesZeroToEveryStat()
    {
        var def = default(CosmeticContext);
        Assert.Equal(0, StatCalculator.StellarCoreAttackPowerContribution(def));
        Assert.Equal(0, StatCalculator.StellarCoreDefensePowerContribution(def));
        Assert.Equal(0, StatCalculator.StellarCoreCriticalDefenceContribution(def));
        Assert.Equal(0, StatCalculator.StellarCoreElementAttackContribution(def));
        Assert.Equal(0, StatCalculator.StellarCoreElementDefenseContribution(def));
        Assert.Equal(0, StatCalculator.StellarCoreMaxLifeContribution(def));
    }

    [Fact]
    public void InvalidIds_JustOutsideEachBand_ContributeZeroToEveryStat()
    {
        foreach (var id in InvalidIds)
        {
            var ctx = Core(id);
            Assert.Equal(0, StatCalculator.StellarCoreAttackPowerContribution(ctx));
            Assert.Equal(0, StatCalculator.StellarCoreDefensePowerContribution(ctx));
            Assert.Equal(0, StatCalculator.StellarCoreCriticalDefenceContribution(ctx));
            Assert.Equal(0, StatCalculator.StellarCoreElementAttackContribution(ctx));
            Assert.Equal(0, StatCalculator.StellarCoreElementDefenseContribution(ctx));
            Assert.Equal(0, StatCalculator.StellarCoreMaxLifeContribution(ctx));
        }
    }


    [Fact]
    public void EveryValidCore_GrantsNonZeroDamageDefenseAndElement()
    {
        foreach (var id in (int[])[.. Tier1Ids, .. Tier2Ids])
        {
            var ctx = Core(id);
            Assert.True(StatCalculator.StellarCoreAttackPowerContribution(ctx) > 0);
            Assert.True(StatCalculator.StellarCoreDefensePowerContribution(ctx) > 0);
            Assert.True(StatCalculator.StellarCoreElementAttackContribution(ctx) > 0);
            Assert.True(StatCalculator.StellarCoreElementDefenseContribution(ctx) > 0);
        }
    }
}
