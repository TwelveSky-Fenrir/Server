using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Tests.Stats;

public class MountGradeContributionTests
{
    [Theory]
    [InlineData(1301)]
    [InlineData(8301)]
    [InlineData(7001)]
    public void TryGetMountBaseRow_TigerTier1Family_AllThreeIdsShareIdenticalColumns(int mountItemId)
    {
        var found = StatCalculator.TryGetMountBaseRow(mountItemId, out var row);

        Assert.True(found);
        Assert.Equal(new StatCalculator.MountBaseRow(0, 5, 5, 0, 0, 0, 0, 5, 0, 30, 0, 24), row);
    }

    [Theory]
    [InlineData(1303, 5)]
    [InlineData(1306, 10)]
    [InlineData(1309, 15)]
    [InlineData(1323, 5)]
    [InlineData(1324, 10)]
    [InlineData(1325, 15)]
    public void TryGetMountBaseRow_DeerAndWolfFamilies_CarryNonzeroCriticalColumn(int mountItemId, int expectedCritical)
    {
        var found = StatCalculator.TryGetMountBaseRow(mountItemId, out var row);

        Assert.True(found);
        Assert.Equal(expectedCritical, row.CriticalColumn);
    }

    [Theory]
    [InlineData(510, 15)]
    [InlineData(511, 15)]
    public void TryGetMountBaseRow_ChristmasEventIds_CarryNonzeroCriticalColumn(int mountItemId, int expectedCritical)
    {
        var found = StatCalculator.TryGetMountBaseRow(mountItemId, out var row);

        Assert.True(found);
        Assert.Equal(expectedCritical, row.CriticalColumn);
        Assert.Equal(-1, row.AbilityEffectId);
    }

    [Theory]
    [InlineData(685)]
    [InlineData(683)]
    [InlineData(684)]
    [InlineData(1451)]
    public void TryGetMountBaseRow_FamilyExceptionIds_HaveAbsorbZeroedAndNoAbilityEffect(int mountItemId)
    {
        var found = StatCalculator.TryGetMountBaseRow(mountItemId, out var row);

        Assert.True(found);
        Assert.Equal(0, row.AbsorbValue);
        Assert.Equal(-1, row.AbilityEffectId);
    }

    [Theory]
    [InlineData(1307, 10)]
    [InlineData(1308, 10)]
    [InlineData(1322, 10)]
    [InlineData(1313, 30)]
    public void TryGetMountBaseRow_CanonicalSiblingsOfExceptionIds_KeepTheirTierAbsorbAndAbilityEffect(
        int mountItemId, int expectedAbsorb)
    {
        var found = StatCalculator.TryGetMountBaseRow(mountItemId, out var row);

        Assert.True(found);
        Assert.Equal(expectedAbsorb, row.AbsorbValue);
        Assert.NotEqual(-1, row.AbilityEffectId);
    }

    [Theory]
    [InlineData(1332, 48)]
    [InlineData(1341, 57)]
    public void TryGetMountBaseRow_PumaTier3Recolors_ShareStatsButVaryOnlyModelId(int mountItemId, int expectedModelId)
    {
        var found = StatCalculator.TryGetMountBaseRow(mountItemId, out var row);

        Assert.True(found);
        Assert.Equal(new StatCalculator.MountBaseRow(20, 20, 0, 0, 0, 0, 0, 0, 0, 5, expectedModelId, 48), row);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999999)]
    public void TryGetMountBaseRow_UnmatchedId_ReturnsFalseAndDefaultRow(int mountItemId)
    {
        var found = StatCalculator.TryGetMountBaseRow(mountItemId, out var row);

        Assert.False(found);
        Assert.Equal(default, row);
    }

    [Fact]
    public void MountBaseDataByItemId_HasExactlyNinetyFourRows()
    {
        int[] allCatalogedIds =
        [
            1301, 8301, 7001, 1302, 8302, 1303, 8303, 1304, 8304, 559, 17044, 1305, 8305, 17045, 1306, 8306,
            17046, 1307, 8307, 685, 814, 1308, 8308, 683, 819, 1309, 8309, 817, 1313, 8313, 1451, 1314, 8314,
            17047, 1315, 8315, 820, 1316, 8316, 510, 511, 1317, 8317, 1318, 8318, 17048, 1319, 8319, 818, 1320,
            8320, 1321, 8321, 17049, 1322, 8322, 684, 821, 17058, 1323, 8323, 1324, 8324, 17050, 1325, 8325,
            815, 1326, 8326, 1327, 8327, 17051, 1328, 8328, 816, 1329, 8329, 17059, 1330, 8330, 17060, 1331,
            8331, 17061, 1332, 1333, 1334, 1335, 1336, 1337, 1338, 1339, 1340, 1341
        ];

        Assert.Equal(94, allCatalogedIds.Length);
        Assert.Equal(94, allCatalogedIds.Distinct().Count());

        foreach (var id in allCatalogedIds) Assert.True(StatCalculator.TryGetMountBaseRow(id, out _));
    }


    [Theory]
    [InlineData(101, 5, 106)]
    [InlineData(103, 10, 113)]
    [InlineData(107, 15, 123)]
    [InlineData(251, 20, 301)]
    public void ApplyMountGradeMultiplierFourTier_RecognizedColumn_MultipliesAndTruncates(
        int total, int column, int expected)
    {
        Assert.Equal(expected, StatCalculator.ApplyMountGradeMultiplierFourTier(total, column));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(25)]
    public void ApplyMountGradeMultiplierFourTier_UnrecognizedColumn_LeavesTotalUnchanged(int column)
    {
        Assert.Equal(500, StatCalculator.ApplyMountGradeMultiplierFourTier(500, column));
    }

    [Fact]
    public void ApplyMountGradeMultiplierFourTier_TruncatesTowardZero_DoesNotRoundToNearest()
    {
        Assert.Equal(128, StatCalculator.ApplyMountGradeMultiplierFourTier(117, 10));
    }


    [Theory]
    [InlineData(101, 5, 106)]
    [InlineData(103, 10, 113)]
    [InlineData(107, 15, 123)]
    public void ApplyMountGradeMultiplierThreeTier_RecognizedColumn_MultipliesAndTruncates(
        int total, int column, int expected)
    {
        Assert.Equal(expected, StatCalculator.ApplyMountGradeMultiplierThreeTier(total, column));
    }

    [Fact]
    public void ApplyMountGradeMultiplierThreeTier_Column20_NotRecognized_LeavesTotalUnchanged()
    {
        Assert.Equal(251, StatCalculator.ApplyMountGradeMultiplierThreeTier(251, 20));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(20)]
    public void ApplyMountGradeMultiplierThreeTier_UnrecognizedColumn_LeavesTotalUnchanged(int column)
    {
        Assert.Equal(500, StatCalculator.ApplyMountGradeMultiplierThreeTier(500, column));
    }


    [Fact]
    public void MountFlatBonuses_PositiveDigit_GrantCitedPerPointMultiple()
    {
        Assert.Equal(300, StatCalculator.MountFlatMaxLifeBonus(3));
        Assert.Equal(600, StatCalculator.MountFlatMaxManaBonus(3));
        Assert.Equal(200, StatCalculator.MountFlatAttackBonus(4));
        Assert.Equal(200, StatCalculator.MountFlatDefenseBonus(2));
        Assert.Equal(500, StatCalculator.MountFlatHitBonus(5));
        Assert.Equal(100, StatCalculator.MountFlatDodgeBonus(1));
        Assert.Equal(300, StatCalculator.MountFlatElementAttackBonus(6));
        Assert.Equal(450, StatCalculator.MountFlatElementDefenseBonus(9));
    }

    [Fact]
    public void MountFlatBonuses_ZeroDigit_GrantNothing()
    {
        Assert.Equal(0, StatCalculator.MountFlatMaxLifeBonus(0));
        Assert.Equal(0, StatCalculator.MountFlatMaxManaBonus(0));
        Assert.Equal(0, StatCalculator.MountFlatAttackBonus(0));
        Assert.Equal(0, StatCalculator.MountFlatDefenseBonus(0));
        Assert.Equal(0, StatCalculator.MountFlatHitBonus(0));
        Assert.Equal(0, StatCalculator.MountFlatDodgeBonus(0));
        Assert.Equal(0, StatCalculator.MountFlatElementAttackBonus(0));
        Assert.Equal(0, StatCalculator.MountFlatElementDefenseBonus(0));
    }

    [Fact]
    public void MountFlatBonuses_NegativeDigit_GrantNothing()
    {
        Assert.Equal(0, StatCalculator.MountFlatMaxLifeBonus(-1));
        Assert.Equal(0, StatCalculator.MountFlatAttackBonus(-9));
    }

    [Fact]
    public void MountFlatMaxManaBonus_MaxDigit_UsesLargestPerPointMultiple()
    {
        Assert.Equal(1800, StatCalculator.MountFlatMaxManaBonus(9));
    }


    [Fact]
    public void DecodeMountPowerDigits_ActivityPositive_AssignsEachDigitToItsFixedStat()
    {
        var digits = StatCalculator.DecodeMountPowerDigits(12_345_678, 1);

        Assert.Equal(1, digits.Attack);
        Assert.Equal(2, digits.Defense);
        Assert.Equal(3, digits.MaxLife);
        Assert.Equal(4, digits.MaxMana);
        Assert.Equal(5, digits.Hit);
        Assert.Equal(6, digits.Dodge);
        Assert.Equal(7, digits.ElementAttack);
        Assert.Equal(8, digits.ElementDefense);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DecodeMountPowerDigits_ActivityNotStrictlyPositive_EveryDigitIsZero(int activity)
    {
        var digits = StatCalculator.DecodeMountPowerDigits(12_345_678, activity);

        Assert.Equal(default, digits);
    }

    [Fact]
    public void DecodeMountPowerDigits_NinthDigitAndAbove_NeverExamined()
    {
        var digits = StatCalculator.DecodeMountPowerDigits(123_456_789, 1);

        Assert.Equal(2, digits.Attack);
        Assert.Equal(3, digits.Defense);
        Assert.Equal(4, digits.MaxLife);
        Assert.Equal(5, digits.MaxMana);
        Assert.Equal(6, digits.Hit);
        Assert.Equal(7, digits.Dodge);
        Assert.Equal(8, digits.ElementAttack);
        Assert.Equal(9, digits.ElementDefense);
    }

    [Fact]
    public void DecodeMountPowerDigits_ZeroPower_EveryDigitIsZeroEvenWithActivity()
    {
        Assert.Equal(default, StatCalculator.DecodeMountPowerDigits(0, 1));
    }

    [Fact]
    public void ComputeMountFlatBonuses_ComposesDecodeWithEachPerStatMultiple()
    {
        var bonuses = StatCalculator.ComputeMountFlatBonuses(12_345_678, 1);

        Assert.Equal(300, bonuses.MaxLife);
        Assert.Equal(800, bonuses.MaxMana);
        Assert.Equal(50, bonuses.Attack);
        Assert.Equal(200, bonuses.Defense);
        Assert.Equal(500, bonuses.Hit);
        Assert.Equal(600, bonuses.Dodge);
        Assert.Equal(350, bonuses.ElementAttack);
        Assert.Equal(400, bonuses.ElementDefense);
    }

    [Fact]
    public void ComputeMountFlatBonuses_ActivityNotPositive_EveryBonusIsZero()
    {
        var bonuses = StatCalculator.ComputeMountFlatBonuses(12_345_678, 0);

        Assert.Equal(default, bonuses);
    }

    [Fact]
    public void ComputeMountFlatBonuses_TierOneUnaffectedByActivityGate_CallerMustApplySeparately()
    {
        var bonuses = StatCalculator.ComputeMountFlatBonuses(12_345_678, 0);

        Assert.Equal(default, bonuses);
        Assert.Equal(220, StatCalculator.ApplyMountGradeMultiplierFourTier(200, 10));
    }


    [Fact]
    public void MountAbsorbPrimaryBonus_MountSetAndAbsorbActive_ReturnsAbsorbValue()
    {
        var mount = new MountContext(1234, AbsorbActive: true, AbsorbValue: 500);
        Assert.Equal(500, StatCalculator.MountAbsorbPrimaryBonus(mount));
    }

    [Fact]
    public void MountAbsorbPrimaryBonus_AbsorbInactive_ReturnsZero()
    {
        var mount = new MountContext(1234, AbsorbActive: false, AbsorbValue: 500);
        Assert.Equal(0, StatCalculator.MountAbsorbPrimaryBonus(mount));
    }

    [Fact]
    public void MountAbsorbPrimaryBonus_NoMountSet_ReturnsZero()
    {
        var mount = new MountContext(0, AbsorbActive: true, AbsorbValue: 500);
        Assert.Equal(0, StatCalculator.MountAbsorbPrimaryBonus(mount));
    }

    [Fact]
    public void MountAbsorbPrimaryBonus_ZeroAbsorbValue_ReturnsZero()
    {
        var mount = new MountContext(1234, AbsorbActive: true, AbsorbValue: 0);
        Assert.Equal(0, StatCalculator.MountAbsorbPrimaryBonus(mount));
    }

    [Fact]
    public void MountAbsorbPrimaryBonus_DefaultContext_ReturnsZero()
    {
        Assert.Equal(0, StatCalculator.MountAbsorbPrimaryBonus(default));
    }
}
