using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class FormationCombatResolverTests
{

    [Fact]
    public void ScaleAttackPower_Code1_ScalesUpByOneTenth_IntegerTruncated()
    {
        Assert.Equal(1100, FormationCombatResolver.ScaleAttackPower(1000, FormationCombatResolver.AttackerPowerBoostCode));
        Assert.Equal(13579, FormationCombatResolver.ScaleAttackPower(12345, FormationCombatResolver.AttackerPowerBoostCode));
        Assert.Equal(107 + 10, FormationCombatResolver.ScaleAttackPower(107, FormationCombatResolver.AttackerPowerBoostCode));
    }

    [Theory]
    [InlineData(FormationCombatResolver.NoFormation)]
    [InlineData(FormationCombatResolver.DefenderDefenseBoostCode)]
    [InlineData(FormationCombatResolver.AttackerCriticalBoostCode)]
    [InlineData(FormationCombatResolver.DefenderCriticalReductionCode)]
    [InlineData((byte)9)]
    public void ScaleAttackPower_NonCode1_LeavesAttackPowerUnchanged(byte code)
    {
        Assert.Equal(1000, FormationCombatResolver.ScaleAttackPower(1000, code));
    }


    [Fact]
    public void ScaleDefensePower_Code2_ScalesUpByOneTenth()
    {
        Assert.Equal(880, FormationCombatResolver.ScaleDefensePower(800, FormationCombatResolver.DefenderDefenseBoostCode));
    }

    [Theory]
    [InlineData(FormationCombatResolver.NoFormation)]
    [InlineData(FormationCombatResolver.AttackerPowerBoostCode)]
    [InlineData(FormationCombatResolver.AttackerCriticalBoostCode)]
    [InlineData(FormationCombatResolver.DefenderCriticalReductionCode)]
    public void ScaleDefensePower_NonCode2_LeavesDefensePowerUnchanged(byte code)
    {
        Assert.Equal(800, FormationCombatResolver.ScaleDefensePower(800, code));
    }


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
        Assert.Equal(0, FormationCombatResolver.CriticalThresholdDelta(
            FormationCombatResolver.DefenderCriticalReductionCode, FormationCombatResolver.AttackerCriticalBoostCode));
    }


    [Fact]
    public void AdjustCriticalChance_NoFormation_IsBaseFlooredAtZero()
    {
        Assert.Equal(18, FormationCombatResolver.AdjustCriticalChance(30, 12,
            FormationCombatResolver.NoFormation, FormationCombatResolver.NoFormation));
        Assert.Equal(0, FormationCombatResolver.AdjustCriticalChance(12, 30,
            FormationCombatResolver.NoFormation, FormationCombatResolver.NoFormation));
    }

    [Fact]
    public void AdjustCriticalChance_FloorHappensBeforeFormationAdd()
    {
        Assert.Equal(5, FormationCombatResolver.AdjustCriticalChance(8, 10,
            FormationCombatResolver.AttackerCriticalBoostCode, FormationCombatResolver.NoFormation));
    }

    [Fact]
    public void AdjustCriticalChance_Code4_CanPushResultNegative_ForTheCallerGuard()
    {
        Assert.Equal(-2, FormationCombatResolver.AdjustCriticalChance(3, 0,
            FormationCombatResolver.NoFormation, FormationCombatResolver.DefenderCriticalReductionCode));
    }

    [Fact]
    public void AdjustCriticalChance_BothCritCodes_CancelToUnadjustedBase()
    {
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
