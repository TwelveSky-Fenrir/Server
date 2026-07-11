using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

/// <summary>
///     Covers <see cref="FormationCombatResolver" /> -- branch A of the B10 contract: the tribe Formation
///     ability (<c>mTribeMasterCallAbility</c>) attack/defense x1.1 scaling and the +/-5 critical-threshold
///     adjustments, enemy-class only (<c>Server/ts25zone/S07_MyGame02.cpp:1068-1256</c>).
/// </summary>
public class FormationCombatResolverTests
{
    // ---- attack power (code 1) ----

    [Fact]
    public void ScaleAttackPower_Code1_ScalesUpByOneTenth_IntegerTruncated()
    {
        // 1000 + 1000/10 = 1100.
        Assert.Equal(1100, FormationCombatResolver.ScaleAttackPower(1000, FormationCombatResolver.AttackerPowerBoostCode));
        // 12345 + 1234 = 13579 (integer truncation of the tenth, not 13579.5).
        Assert.Equal(13579, FormationCombatResolver.ScaleAttackPower(12345, FormationCombatResolver.AttackerPowerBoostCode));
        // value + value/10 == value*11/10 for a value the tenth doesn't divide evenly.
        Assert.Equal(107 + 10, FormationCombatResolver.ScaleAttackPower(107, FormationCombatResolver.AttackerPowerBoostCode));
    }

    [Theory]
    [InlineData(FormationCombatResolver.NoFormation)]
    [InlineData(FormationCombatResolver.DefenderDefenseBoostCode)] // code 2 is the DEFENDER's, never boosts attack
    [InlineData(FormationCombatResolver.AttackerCriticalBoostCode)] // code 3 only touches the crit threshold
    [InlineData(FormationCombatResolver.DefenderCriticalReductionCode)]
    [InlineData((byte)9)] // any out-of-range code is inert
    public void ScaleAttackPower_NonCode1_LeavesAttackPowerUnchanged(byte code)
    {
        Assert.Equal(1000, FormationCombatResolver.ScaleAttackPower(1000, code));
    }

    // ---- defense power (code 2) ----

    [Fact]
    public void ScaleDefensePower_Code2_ScalesUpByOneTenth()
    {
        Assert.Equal(880, FormationCombatResolver.ScaleDefensePower(800, FormationCombatResolver.DefenderDefenseBoostCode));
    }

    [Theory]
    [InlineData(FormationCombatResolver.NoFormation)]
    [InlineData(FormationCombatResolver.AttackerPowerBoostCode)] // code 1 is the ATTACKER's, never boosts defense
    [InlineData(FormationCombatResolver.AttackerCriticalBoostCode)]
    [InlineData(FormationCombatResolver.DefenderCriticalReductionCode)]
    public void ScaleDefensePower_NonCode2_LeavesDefensePowerUnchanged(byte code)
    {
        Assert.Equal(800, FormationCombatResolver.ScaleDefensePower(800, code));
    }

    // ---- critical-threshold delta (codes 3 / 4) ----

    [Fact]
    public void CriticalThresholdDelta_AttackerHolds3_DefenderNot4_RaisesByFive()
    {
        Assert.Equal(5, FormationCombatResolver.CriticalThresholdDelta(
            FormationCombatResolver.AttackerCriticalBoostCode, FormationCombatResolver.NoFormation));
    }

    [Fact]
    public void CriticalThresholdDelta_DefenderHolds4_AttackerNot3_LowersByFive()
    {
        Assert.Equal(-5, FormationCombatResolver.CriticalThresholdDelta(
            FormationCombatResolver.NoFormation, FormationCombatResolver.DefenderCriticalReductionCode));
    }

    [Fact]
    public void CriticalThresholdDelta_BothPresent_CancelToZero()
    {
        Assert.Equal(0, FormationCombatResolver.CriticalThresholdDelta(
            FormationCombatResolver.AttackerCriticalBoostCode, FormationCombatResolver.DefenderCriticalReductionCode));
    }

    [Fact]
    public void CriticalThresholdDelta_NeitherPresent_IsZero()
    {
        Assert.Equal(0, FormationCombatResolver.CriticalThresholdDelta(
            FormationCombatResolver.NoFormation, FormationCombatResolver.NoFormation));
        // Attacker holding 4 / defender holding 3 are the WRONG side for those codes -> inert.
        Assert.Equal(0, FormationCombatResolver.CriticalThresholdDelta(
            FormationCombatResolver.DefenderCriticalReductionCode, FormationCombatResolver.AttackerCriticalBoostCode));
    }

    // ---- full critical-chance resolution (floor-then-adjust) ----

    [Fact]
    public void AdjustCriticalChance_NoFormation_IsBaseFlooredAtZero()
    {
        // 30 - 12 = 18, no formation.
        Assert.Equal(18, FormationCombatResolver.AdjustCriticalChance(30, 12,
            FormationCombatResolver.NoFormation, FormationCombatResolver.NoFormation));
        // 12 - 30 = -18 floored to 0 (the caller's ">0" guard treats 0 as no-crit, matching the raw negative).
        Assert.Equal(0, FormationCombatResolver.AdjustCriticalChance(12, 30,
            FormationCombatResolver.NoFormation, FormationCombatResolver.NoFormation));
    }

    [Fact]
    public void AdjustCriticalChance_FloorHappensBeforeFormationAdd()
    {
        // Base 8 - 10 = -2 -> floored to 0 -> attacker code 3 adds +5 -> 5 (NOT -2 + 5 = 3).
        Assert.Equal(5, FormationCombatResolver.AdjustCriticalChance(8, 10,
            FormationCombatResolver.AttackerCriticalBoostCode, FormationCombatResolver.NoFormation));
    }

    [Fact]
    public void AdjustCriticalChance_Code4_CanPushResultNegative_ForTheCallerGuard()
    {
        // Base 3, defender code 4 -> 3 - 5 = -2 (the ">0" guard at the call site then skips the crit roll).
        Assert.Equal(-2, FormationCombatResolver.AdjustCriticalChance(3, 0,
            FormationCombatResolver.NoFormation, FormationCombatResolver.DefenderCriticalReductionCode));
    }

    [Fact]
    public void AdjustCriticalChance_BothCritCodes_CancelToUnadjustedBase()
    {
        // Base 20, attacker 3 + defender 4 cancel -> 20.
        Assert.Equal(20, FormationCombatResolver.AdjustCriticalChance(40, 20,
            FormationCombatResolver.AttackerCriticalBoostCode, FormationCombatResolver.DefenderCriticalReductionCode));
    }

    [Fact]
    public void FormationCodeConstants_MatchLegacyValues()
    {
        Assert.Equal((byte)0, FormationCombatResolver.NoFormation);
        Assert.Equal((byte)1, FormationCombatResolver.AttackerPowerBoostCode);
        Assert.Equal((byte)2, FormationCombatResolver.DefenderDefenseBoostCode);
        Assert.Equal((byte)3, FormationCombatResolver.AttackerCriticalBoostCode);
        Assert.Equal((byte)4, FormationCombatResolver.DefenderCriticalReductionCode);
        Assert.Equal(5, FormationCombatResolver.CriticalThresholdShift);
    }
}
