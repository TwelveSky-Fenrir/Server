using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Tests.Combat;

/// <summary>
///     Covers <see cref="FfaEqualStatsOverride" /> -- the FFA zone (335) "equal real stats" override
///     (<c>Server/Header/Protocol/MyFactor.cpp:58-96</c>, <c>:4009-4478</c>).
/// </summary>
public class FfaEqualStatsOverrideTests
{
    private static EffectiveStats RealStats()
    {
        return new EffectiveStats(
            5000,
            2000,
            900,
            400,
            250,
            60,
            30,
            10,
            5,
            120,
            40);
    }

    [Fact]
    public void FfaZone_OverridesAllNineCombatStats_ToFixedConstants()
    {
        var overridden = FfaEqualStatsOverride.Apply(PvpKillRewardZoneCatalog.FfaMapNumber, RealStats());

        Assert.Equal(45000, overridden.AttackPower);
        Assert.Equal(30000, overridden.DefensePower);
        Assert.Equal(30000, overridden.AttackSuccess);
        Assert.Equal(5000, overridden.AttackBlock);
        Assert.Equal(1000, overridden.ElementAttackPower);
        Assert.Equal(500, overridden.ElementDefensePower);
        Assert.Equal(100, overridden.Critical);
        Assert.Equal(65, overridden.CriticalDefence);
        Assert.Equal(300, overridden.Luck);
    }

    [Fact]
    public void FfaZone_LeavesMaxLifeAndMaxMana_Untouched()
    {
        var real = RealStats();
        var overridden = FfaEqualStatsOverride.Apply(PvpKillRewardZoneCatalog.FfaMapNumber, real);

        Assert.Equal(real.MaxLife, overridden.MaxLife);
        Assert.Equal(real.MaxMana, overridden.MaxMana);
    }

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)49)]
    [InlineData((short)324)]
    [InlineData((short)334)]
    [InlineData((short)336)]
    public void NonFfaZone_ReturnsRealStats_Unchanged(short zoneId)
    {
        var real = RealStats();
        var result = FfaEqualStatsOverride.Apply(zoneId, real);

        Assert.Equal(real, result);
    }

    [Fact]
    public void IsEqualStatsZone_TrueOnlyForFfaMapNumber()
    {
        Assert.True(FfaEqualStatsOverride.IsEqualStatsZone(PvpKillRewardZoneCatalog.FfaMapNumber));
        Assert.False(FfaEqualStatsOverride.IsEqualStatsZone(334));
        Assert.False(FfaEqualStatsOverride.IsEqualStatsZone(336));
    }

    [Fact]
    public void FfaZone_EveryCombatantGetsTheIdenticalFixedStats_RegardlessOfRealInput()
    {
        var geared = new EffectiveStats(9999, 9999, 50000, 40000, 500, 200, 90, 80, 400, 300, 150);
        var undergeared = new EffectiveStats(100, 50, 5, 2, 10, 1, 0, 0, 0, 0, 0);

        var gearedOverridden = FfaEqualStatsOverride.Apply(PvpKillRewardZoneCatalog.FfaMapNumber, geared);
        var undergearedOverridden = FfaEqualStatsOverride.Apply(PvpKillRewardZoneCatalog.FfaMapNumber, undergeared);

        Assert.Equal(gearedOverridden.AttackPower, undergearedOverridden.AttackPower);
        Assert.Equal(gearedOverridden.DefensePower, undergearedOverridden.DefensePower);
        Assert.Equal(gearedOverridden.AttackSuccess, undergearedOverridden.AttackSuccess);
        Assert.Equal(gearedOverridden.AttackBlock, undergearedOverridden.AttackBlock);
        Assert.Equal(gearedOverridden.Critical, undergearedOverridden.Critical);
        Assert.Equal(gearedOverridden.CriticalDefence, undergearedOverridden.CriticalDefence);
        Assert.Equal(gearedOverridden.Luck, undergearedOverridden.Luck);
        Assert.Equal(gearedOverridden.ElementAttackPower, undergearedOverridden.ElementAttackPower);
        Assert.Equal(gearedOverridden.ElementDefensePower, undergearedOverridden.ElementDefensePower);
    }
}
