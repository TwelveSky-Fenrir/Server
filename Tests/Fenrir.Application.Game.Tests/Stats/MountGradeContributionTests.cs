using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Exercises the recoverable, fully-cited primitives of workstream B8-mount
///     (<see cref="StatCalculator" /> mount grade contributions) directly, since the getter-body call sites
///     are deferred to the serial integration pass (and Tier-1 / Tier-2 stay data-blocked on the unrecoverable
///     MyAnimal base table -- see the workstream's openQuestions). Covers the Tier-1 grade percentage
///     multiplier (four-tier vs three-tier, and its truncate-toward-zero semantics), the Tier-2 flat
///     per-point additive multipliers, and the Tier-2b absorb-to-primary rule.
/// </summary>
public class MountGradeContributionTests
{
    // ---- Tier 1: grade percentage multiplier (four-tier: HP/MP/attack/defense) ----

    [Theory]
    [InlineData(101, 5, 106)] // 101 * 1.05f = 106.05  -> 106
    [InlineData(103, 10, 113)] // 103 * 1.10f = 113.30  -> 113
    [InlineData(107, 15, 123)] // 107 * 1.15f = 123.05  -> 123
    [InlineData(251, 20, 301)] // 251 * 1.20f = 301.20  -> 301
    public void ApplyMountGradeMultiplierFourTier_RecognizedColumn_MultipliesAndTruncates(
        int total, int column, int expected)
    {
        Assert.Equal(expected, StatCalculator.ApplyMountGradeMultiplierFourTier(total, column));
    }

    [Theory]
    [InlineData(0)] // no mount / no column for this stat
    [InlineData(3)] // nonzero but not a recognized tier -> falls through
    [InlineData(7)]
    [InlineData(25)]
    public void ApplyMountGradeMultiplierFourTier_UnrecognizedColumn_LeavesTotalUnchanged(int column)
    {
        Assert.Equal(500, StatCalculator.ApplyMountGradeMultiplierFourTier(500, column));
    }

    [Fact]
    public void ApplyMountGradeMultiplierFourTier_TruncatesTowardZero_DoesNotRoundToNearest()
    {
        // 117 * 1.10f = 128.70 -- round-to-nearest would give 129; the legacy (int) cast truncates to 128.
        Assert.Equal(128, StatCalculator.ApplyMountGradeMultiplierFourTier(117, 10));
    }

    // ---- Tier 1: grade percentage multiplier (three-tier: hit/dodge/critical/element-atk/element-def) ----

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
        // The five three-tier stats have no 20-case; a column of 20 must fall through to no multiply.
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

    // ---- Tier 2: flat per-point additive bonuses ----

    [Fact]
    public void MountFlatBonuses_PositiveDigit_GrantCitedPerPointMultiple()
    {
        Assert.Equal(300, StatCalculator.MountFlatMaxLifeBonus(3)); // +100/digit
        Assert.Equal(600, StatCalculator.MountFlatMaxManaBonus(3)); // +200/digit
        Assert.Equal(200, StatCalculator.MountFlatAttackBonus(4)); // +50/digit
        Assert.Equal(200, StatCalculator.MountFlatDefenseBonus(2)); // +100/digit
        Assert.Equal(500, StatCalculator.MountFlatHitBonus(5)); // +100/digit
        Assert.Equal(100, StatCalculator.MountFlatDodgeBonus(1)); // +100/digit
        Assert.Equal(300, StatCalculator.MountFlatElementAttackBonus(6)); // +50/digit
        Assert.Equal(450, StatCalculator.MountFlatElementDefenseBonus(9)); // +50/digit
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
        // The per-digit guard is "strictly greater than zero"; an out-of-domain negative never subtracts.
        Assert.Equal(0, StatCalculator.MountFlatMaxLifeBonus(-1));
        Assert.Equal(0, StatCalculator.MountFlatAttackBonus(-9));
    }

    [Fact]
    public void MountFlatMaxManaBonus_MaxDigit_UsesLargestPerPointMultiple()
    {
        // MP is the only +200/digit stat; a full 9-digit is 1800.
        Assert.Equal(1800, StatCalculator.MountFlatMaxManaBonus(9));
    }

    // ---- Tier 2's own input: the per-digit power decode (workstream B8-myanimal-table) ----

    [Fact]
    public void DecodeMountPowerDigits_ActivityPositive_AssignsEachDigitToItsFixedStat()
    {
        // 12_345_678 -- reading place-by-place left to right: ten-millions=1, millions=2, hundred-thousands=3,
        // ten-thousands=4, thousands=5, hundreds=6, tens=7, ones=8. Contract mapping: ten-millions=attack,
        // millions=defense, hundred-thousands=max-life, ten-thousands=max-mana, thousands=hit, hundreds=dodge,
        // tens=element-attack, ones=element-defense.
        var digits = StatCalculator.DecodeMountPowerDigits(12_345_678, activity: 1);

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
        // 123_456_789 has a 9-digit magnitude; the hundred-millionth's-place "1" must never surface anywhere
        // -- only the low 8 decimal places (23_456_789's own low 8) are read.
        var digits = StatCalculator.DecodeMountPowerDigits(123_456_789, activity: 1);

        Assert.Equal(2, digits.Attack); // ten-millions place of ...23456789 is 2, not the dropped leading 1
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
        Assert.Equal(default, StatCalculator.DecodeMountPowerDigits(0, activity: 1));
    }

    [Fact]
    public void ComputeMountFlatBonuses_ComposesDecodeWithEachPerStatMultiple()
    {
        var bonuses = StatCalculator.ComputeMountFlatBonuses(12_345_678, activity: 1);

        Assert.Equal(300, bonuses.MaxLife); // digit 3 * 100
        Assert.Equal(800, bonuses.MaxMana); // digit 4 * 200
        Assert.Equal(50, bonuses.Attack); // digit 1 * 50
        Assert.Equal(200, bonuses.Defense); // digit 2 * 100
        Assert.Equal(500, bonuses.Hit); // digit 5 * 100
        Assert.Equal(600, bonuses.Dodge); // digit 6 * 100
        Assert.Equal(350, bonuses.ElementAttack); // digit 7 * 50
        Assert.Equal(400, bonuses.ElementDefense); // digit 8 * 50
    }

    [Fact]
    public void ComputeMountFlatBonuses_ActivityNotPositive_EveryBonusIsZero()
    {
        var bonuses = StatCalculator.ComputeMountFlatBonuses(12_345_678, activity: 0);

        Assert.Equal(default, bonuses);
    }

    [Fact]
    public void ComputeMountFlatBonuses_TierOneUnaffectedByActivityGate_CallerMustApplySeparately()
    {
        // Tier 1's grade-percentage multiply is driven purely by the static table column, never by activity --
        // ComputeMountFlatBonuses only ever produces Tier 2's own additive amounts, so a Tier-1 multiply must
        // still be applied by the caller even when activity gates every Tier-2 flat bonus to zero.
        var bonuses = StatCalculator.ComputeMountFlatBonuses(12_345_678, activity: 0);

        Assert.Equal(default, bonuses);
        Assert.Equal(220, StatCalculator.ApplyMountGradeMultiplierFourTier(200, column: 10));
    }

    // ---- Tier 2b: absorb -> primary stats ----

    [Fact]
    public void MountAbsorbPrimaryBonus_MountSetAndAbsorbActive_ReturnsAbsorbValue()
    {
        var mount = new MountContext(AnimalNumber: 1234, AbsorbActive: true, AbsorbValue: 500);
        Assert.Equal(500, StatCalculator.MountAbsorbPrimaryBonus(mount));
    }

    [Fact]
    public void MountAbsorbPrimaryBonus_AbsorbInactive_ReturnsZero()
    {
        var mount = new MountContext(AnimalNumber: 1234, AbsorbActive: false, AbsorbValue: 500);
        Assert.Equal(0, StatCalculator.MountAbsorbPrimaryBonus(mount));
    }

    [Fact]
    public void MountAbsorbPrimaryBonus_NoMountSet_ReturnsZero()
    {
        // AnimalNumber 0 means no mount summoned -- even an active absorb flag with a magnitude grants nothing.
        var mount = new MountContext(AnimalNumber: 0, AbsorbActive: true, AbsorbValue: 500);
        Assert.Equal(0, StatCalculator.MountAbsorbPrimaryBonus(mount));
    }

    [Fact]
    public void MountAbsorbPrimaryBonus_ZeroAbsorbValue_ReturnsZero()
    {
        // The common case today: the assembler leaves AbsorbValue 0 until the MyAnimal base table is loaded.
        var mount = new MountContext(AnimalNumber: 1234, AbsorbActive: true, AbsorbValue: 0);
        Assert.Equal(0, StatCalculator.MountAbsorbPrimaryBonus(mount));
    }

    [Fact]
    public void MountAbsorbPrimaryBonus_DefaultContext_ReturnsZero()
    {
        Assert.Equal(0, StatCalculator.MountAbsorbPrimaryBonus(default));
    }
}
