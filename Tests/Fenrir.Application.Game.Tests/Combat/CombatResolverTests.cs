using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Combat;

/// <summary>
///     Covers <see cref="CombatResolver.ResolveEnemyTribeAttack" /> (mCase 2, Avatar vs. enemy-tribe Avatar) against
///     <c>AttackPlayer</c> (<c>Server/ts25zone/S07_MyGame02.cpp</c>), including the deliberately-preserved PvP-only
///     "divide final damage by 5" quirk.
/// </summary>
public class CombatResolverTests
{
    private static CombatantSnapshot Combatant(int characterId, byte tribe, int attackPower = 0,
        int defensePower = 0, int attackSuccess = 100, int attackBlock = 0, int critical = 0,
        int criticalDefence = 0, int elementAttack = 0, int elementDefense = 0, bool isDead = false,
        int life = 100_000, float x = 0, float y = 0, float z = 0, TimeSpan? zoneEntryAt = null,
        int chargeBuffPercent = 0)
    {
        var stats = new EffectiveStats(100_000, 100_000, attackPower, defensePower, attackSuccess, attackBlock,
            critical, criticalDefence, 0, elementAttack, elementDefense);
        return new CombatantSnapshot(characterId, tribe, isDead, life, 100_000, x, y, z, zoneEntryAt, stats,
            chargeBuffPercent);
    }

    private static AttackForProtocol MeleeRequest(int attackerId, int defenderId)
    {
        return new AttackForProtocol
        {
            Case = 2,
            ServerIndex1 = attackerId,
            UniqueNumber1 = unchecked((uint)attackerId),
            ServerIndex2 = defenderId,
            UniqueNumber2 = unchecked((uint)defenderId),
            SenderLocation = [0, 0, 0],
            AttackActionValue1 = 1,
            AttackActionValue2 = 0,
            AttackActionValue3 = 0,
            AttackActionValue4 = 0,
            AttackResultValue = 0,
            AttackCriticalExist = 0,
            AttackElementDamage = 0,
            AttackViewDamageValue = 0,
            AttackRealDamageValue = 0
        };
    }

    [Fact]
    public void SameCharacter_IsRejected()
    {
        var a = Combatant(1, 0);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(a, a, MeleeRequest(1, 1), TimeSpan.Zero, null,
            new ScriptedRandomSource(0));
        Assert.True(outcome.Rejected);
        Assert.Equal(AttackRejectReason.SameCharacter, outcome.RejectReason);
    }

    [Fact]
    public void SameTribe_IsRejected()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 0);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0));
        Assert.True(outcome.Rejected);
        Assert.Equal(AttackRejectReason.SameOrAlliedTribe, outcome.RejectReason);
    }

    [Fact]
    public void DefenderDead_IsRejected()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1, isDead: true);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0));
        Assert.Equal(AttackRejectReason.DefenderDead, outcome.RejectReason);
    }

    [Fact]
    public void OutOfRange_IsRejected()
    {
        var attacker = Combatant(1, 0, x: 0);
        var defender = Combatant(2, 1, x: 300); // > 185 max attack distance
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0));
        Assert.Equal(AttackRejectReason.OutOfRange, outcome.RejectReason);
    }

    [Fact]
    public void DefenderWithinZoneEntryGracePeriod_IsRejected()
    {
        // PROTECT_TICK is a one-shot spawn/arrival grace period, not a rolling "recently damaged" cooldown
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1, zoneEntryAt: TimeSpan.FromSeconds(5));
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2),
            TimeSpan.FromSeconds(6), null, new ScriptedRandomSource(0)); // only 1s since the defender entered
        Assert.Equal(AttackRejectReason.DefenderProtected, outcome.RejectReason);
    }

    [Fact]
    public void BothSidesPastTheirGracePeriod_AttackProceedsNormally()
    {
        // regression guard: landing/receiving a hit must never re-stamp the zone-entry protect window
        var attacker = Combatant(1, 0, 1000, zoneEntryAt: TimeSpan.Zero);
        var defender = Combatant(2, 1, defensePower: 200, zoneEntryAt: TimeSpan.Zero);

        var first = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2),
            TimeSpan.FromSeconds(20), null, new ScriptedRandomSource(0, 0));
        Assert.False(first.Rejected);
        Assert.True(first.Hit);

        var second = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2),
            TimeSpan.FromSeconds(20.5), null, new ScriptedRandomSource(0, 0));
        Assert.False(second.Rejected);
        Assert.True(second.Hit);
    }

    [Fact]
    public void AttackerWithNoAttackSuccess_IsRejected()
    {
        var attacker = Combatant(1, 0, attackSuccess: 0);
        var defender = Combatant(2, 1);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0));
        Assert.Equal(AttackRejectReason.AttackerHasNoAttackSuccess, outcome.RejectReason);
    }

    [Fact]
    public void HigherBlockThanSuccess_CanStillMiss()
    {
        var attacker = Combatant(1, 0, attackSuccess: 50);
        var defender = Combatant(2, 1, attackBlock: 500); // hit chance clamps to 1%
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(50)); // roll 50 >= 1% chance -> miss
        Assert.False(outcome.Rejected);
        Assert.False(outcome.Hit);
        Assert.Equal(0, outcome.DamageApplied);
    }

    [Fact]
    public void NoBlock_AlwaysHits_NoRollConsumed()
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 1, defensePower: 200, attackBlock: 0);
        // Only 2 draws available (variance dir/mag) -- if a hit-chance roll were (wrongly) consumed here, the
        // variance draws would shift and this test's exact-value assertion below would fail.
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        Assert.True(outcome.Hit);
        // (1000-200)=800, no variance change (0,0), floor 5 not needed, no crit (0 vs 0), /5 = 160.
        Assert.Equal(160, outcome.DamageApplied);
    }

    [Fact]
    public void Damage_IsAtkMinusDefWithVarianceThenDividedByFive_PvpOnlyQuirk()
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 1, defensePower: 200);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        // (1000-200)=800 -> variance no-op -> min5 floor not needed -> no crit -> /5 = 160 (NOT 800).
        Assert.Equal(160, outcome.DamageApplied);
    }

    [Fact]
    public void Damage_BelowFloor_ClampsToMinimumBeforeDivision()
    {
        var attacker = Combatant(1, 0, 105);
        var defender = Combatant(2, 1, defensePower: 100); // raw damage = 5, no floor needed either way
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        // (105-100)=5 -> variance no-op -> already at floor 5 -> no crit -> /5 = 1 (the PvP damage floor).
        Assert.Equal(1, outcome.DamageApplied);
    }

    [Fact]
    public void Charge_IsAppliedBeforeVarianceAndConsumed()
    {
        var attacker = Combatant(1, 0, 1000, chargeBuffPercent: 50);
        var defender = Combatant(2, 1, defensePower: 200);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        // (1000-200)=800 -> charge x1.5 = 1200 -> variance no-op -> /5 = 240.
        Assert.Equal(240, outcome.DamageApplied);
        Assert.True(outcome.ChargeConsumed);
    }

    [Fact]
    public void Charge_IsConsumedOnAMissToo_NotOnlyOnAHit()
    {
        // AttackPlayer spends the charge buff the moment an attack is attempted, before the hit-chance roll
        var attacker = Combatant(1, 0, 1000, attackSuccess: 1, chargeBuffPercent: 50);
        var defender = Combatant(2, 1, defensePower: 200, attackBlock: 100_000); // hit chance clamps to 1%
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(50)); // roll 50 >= 1% chance -> miss
        Assert.False(outcome.Hit);
        Assert.True(outcome.ChargeConsumed);
    }

    [Fact]
    public void Critical_DoublesDamageBeforeTheFinalDivision()
    {
        var attacker = Combatant(1, 0, 1000, critical: 100);
        var defender = Combatant(2, 1, defensePower: 200, criticalDefence: 0);
        // variance dir=0, variance mag=0, then a crit roll of 50 (< 100% chance) -> crits.
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0, 50));
        Assert.True(outcome.Critical);
        // (1000-200)=800 -> x2 crit = 1600 -> /5 = 320.
        Assert.Equal(320, outcome.DamageApplied);
    }

    [Fact]
    public void ElementDamage_AddsAfterTheFinalDivision_AndIsNotItselfDivided()
    {
        var attacker = Combatant(1, 0, 1000, elementAttack: 300);
        var defender = Combatant(2, 1, defensePower: 200, elementDefense: 100);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        // Base: (1000-200)/5 = 160. Element: 300-100=200, added AFTER the /5 (not itself divided).
        Assert.Equal(200, outcome.ElementDamage);
        Assert.Equal(360, outcome.DamageApplied);
    }

    [Fact]
    public void DamageNeverExceedsDefenderRemainingLife()
    {
        var attacker = Combatant(1, 0, 1_000_000);
        var defender = Combatant(2, 1, defensePower: 0, life: 50);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        Assert.Equal(50, outcome.DamageApplied);
    }
}
