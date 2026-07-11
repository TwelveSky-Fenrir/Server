using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

/// <summary>
///     Covers <see cref="Zone124DuelOverrideResolver" /> -- branch C of the B10 contract: the map-124
///     final-countdown x3-damage / forced-crit duel override
///     (<c>Server/ts25zone/S07_MyGame02.cpp:1146-1150</c>).
/// </summary>
public class Zone124DuelOverrideResolverTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)] // strictly below ten
    public void IsActive_OnMap124_BelowTen_IsActive(int countdown)
    {
        Assert.True(Zone124DuelOverrideResolver.IsActive(isMap124Process: true, countdown));
    }

    [Theory]
    [InlineData(10)] // exactly ten is NOT below ten
    [InlineData(11)]
    [InlineData(60)]
    public void IsActive_OnMap124_AtOrAboveTen_IsInactive(int countdown)
    {
        Assert.False(Zone124DuelOverrideResolver.IsActive(isMap124Process: true, countdown));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(9)]
    public void IsActive_OffMap124_IsAlwaysInactive_EvenBelowTen(int countdown)
    {
        Assert.False(Zone124DuelOverrideResolver.IsActive(isMap124Process: false, countdown));
    }

    [Fact]
    public void Apply_Active_TriplesDamage_AndForcesCrit()
    {
        // The 200 here is already the ordinary-crit-doubled value; the override triples it to 600 (the divide
        // -by-five happens later, at the call site) -- the contract's own 100 -> 200 -> 600 -> 120 illustration.
        var outcome = Zone124DuelOverrideResolver.Apply(damage: 200, critExists: true, isMap124Process: true,
            countdownRemaining: 9);

        Assert.Equal(600, outcome.Damage);
        Assert.True(outcome.CritExists);
    }

    [Fact]
    public void Apply_Active_ForcesCrit_EvenWhenOrdinaryCritDidNotFire()
    {
        // A non-crit base hit still gets the forced crit flag and the triple.
        var outcome = Zone124DuelOverrideResolver.Apply(damage: 100, critExists: false, isMap124Process: true,
            countdownRemaining: 3);

        Assert.Equal(300, outcome.Damage);
        Assert.True(outcome.CritExists);
    }

    [Fact]
    public void Apply_Inactive_LeavesDamageAndCritUnchanged()
    {
        var offMap = Zone124DuelOverrideResolver.Apply(damage: 200, critExists: true, isMap124Process: false,
            countdownRemaining: 3);
        Assert.Equal(200, offMap.Damage);
        Assert.True(offMap.CritExists);

        var aboveThreshold = Zone124DuelOverrideResolver.Apply(damage: 200, critExists: false,
            isMap124Process: true, countdownRemaining: 10);
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
