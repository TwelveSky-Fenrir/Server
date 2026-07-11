using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class Zone124DuelOverrideResolverTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]
    public void IsActive_OnMap124_BelowTen_IsActive(int countdown)
    {
        Assert.True(Zone124DuelOverrideResolver.IsActive(true, countdown));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(60)]
    public void IsActive_OnMap124_AtOrAboveTen_IsInactive(int countdown)
    {
        Assert.False(Zone124DuelOverrideResolver.IsActive(true, countdown));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(9)]
    public void IsActive_OffMap124_IsAlwaysInactive_EvenBelowTen(int countdown)
    {
        Assert.False(Zone124DuelOverrideResolver.IsActive(false, countdown));
    }

    [Fact]
    public void Apply_Active_TriplesDamage_AndForcesCrit()
    {
        var outcome = Zone124DuelOverrideResolver.Apply(200, true, true,
            9);

        Assert.Equal(600, outcome.Damage);
        Assert.True(outcome.CritExists);
    }

    [Fact]
    public void Apply_Active_ForcesCrit_EvenWhenOrdinaryCritDidNotFire()
    {
        var outcome = Zone124DuelOverrideResolver.Apply(100, false, true,
            3);

        Assert.Equal(300, outcome.Damage);
        Assert.True(outcome.CritExists);
    }

    [Fact]
    public void Apply_Inactive_LeavesDamageAndCritUnchanged()
    {
        var offMap = Zone124DuelOverrideResolver.Apply(200, true, false,
            3);
        Assert.Equal(200, offMap.Damage);
        Assert.True(offMap.CritExists);

        var aboveThreshold = Zone124DuelOverrideResolver.Apply(200, false,
            true, 10);
        Assert.Equal(200, aboveThreshold.Damage);
        Assert.False(aboveThreshold.CritExists);
    }

    [Fact]
    public void Constants_MatchLegacyValues()
    {
        Assert.Equal((short)124, Zone124DuelOverrideResolver.Zone124MapId);
        Assert.Equal(10, Zone124DuelOverrideResolver.FinalCountdownThreshold);
        Assert.Equal(3, Zone124DuelOverrideResolver.DamageMultiplier);
    }
}
