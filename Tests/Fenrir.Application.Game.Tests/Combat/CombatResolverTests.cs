using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Combat;

public class CombatResolverTests
{
    private static CombatantSnapshot Combatant(int characterId, byte tribe, int attackPower = 0,
        int defensePower = 0, int attackSuccess = 100, int attackBlock = 0, int critical = 0,
        int criticalDefence = 0, int elementAttack = 0, int elementDefense = 0, bool isDead = false,
        int life = 100_000, float x = 0, float y = 0, float z = 0, TimeSpan? zoneEntryAt = null,
        int chargeBuffPercent = 0, short level = 0)
    {
        var stats = new EffectiveStats(100_000, 100_000, attackPower, defensePower, attackSuccess, attackBlock,
            critical, criticalDefence, 0, elementAttack, elementDefense);
        return new CombatantSnapshot(characterId, tribe, isDead, life, 100_000, x, y, z, zoneEntryAt, stats,
            chargeBuffPercent, level);
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
    public void NormalFieldZone_AttackIsAllowed()
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 1, defensePower: 200);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        Assert.False(outcome.Rejected);
        Assert.True(outcome.Hit);
    }

    [Fact]
    public void TownOrSafeZone_AttackIsRejected_BeforeTribeCheck()
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 1, defensePower: 200);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0), false);
        Assert.True(outcome.Rejected);
        Assert.Equal(AttackRejectReason.ZonePvpDisabled, outcome.RejectReason);
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
    public void SameTribe_OnOpenPvpMap_IsExemptFromRejection()
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 0, defensePower: 200);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0), sameTribeAttackExempt: true);
        Assert.False(outcome.Rejected);
        Assert.True(outcome.Hit);
    }

    [Fact]
    public void AlliedTribe_IsRejected()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0), allyOfAttackerTribe: 1);
        Assert.True(outcome.Rejected);
        Assert.Equal(AttackRejectReason.SameOrAlliedTribe, outcome.RejectReason);
    }

    [Fact]
    public void DifferentTribe_NotTheCurrentAlly_IsNotRejectedByTheAllianceHalf()
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 2, defensePower: 200);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0), allyOfAttackerTribe: 1);
        Assert.False(outcome.Rejected);
        Assert.True(outcome.Hit);
    }

    [Fact]
    public void AlliedTribe_OnOpenPvpMap_IsExemptFromRejection()
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 1, defensePower: 200);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0), sameTribeAttackExempt: true, allyOfAttackerTribe: 1);
        Assert.False(outcome.Rejected);
        Assert.True(outcome.Hit);
    }

    [Fact]
    public void NoActiveAlliance_DifferentTribe_IsNotRejected()
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 1, defensePower: 200);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        Assert.False(outcome.Rejected);
        Assert.True(outcome.Hit);
    }

    [Fact]
    public void NewbieProtection_AttackerAtOrAbove90_DefenderBelow90_IsRejected()
    {
        var attacker = Combatant(1, 0, 1000, level: 90);
        var defender = Combatant(2, 1, defensePower: 200, level: 89);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0), newbieProtectionZone: true);
        Assert.True(outcome.Rejected);
        Assert.Equal(AttackRejectReason.NewbieProtectionLevelGap, outcome.RejectReason);
    }

    [Fact]
    public void NewbieProtection_BothSidesAtOrAbove90_IsNotRejected()
    {
        var attacker = Combatant(1, 0, 1000, level: 90);
        var defender = Combatant(2, 1, defensePower: 200, level: 90);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0), newbieProtectionZone: true);
        Assert.False(outcome.Rejected);
        Assert.True(outcome.Hit);
    }

    [Fact]
    public void NewbieProtection_AttackerBelow90_IsNotRejected_RegardlessOfDefenderLevel()
    {
        var attacker = Combatant(1, 0, 1000, level: 89);
        var defender = Combatant(2, 1, defensePower: 200, level: 1);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0), newbieProtectionZone: true);
        Assert.False(outcome.Rejected);
        Assert.True(outcome.Hit);
    }

    [Fact]
    public void NewbieProtection_OutsideGatedZone_IsNotRejected_EvenAtQualifyingLevels()
    {
        var attacker = Combatant(1, 0, 1000, level: 99);
        var defender = Combatant(2, 1, defensePower: 200, level: 1);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        Assert.False(outcome.Rejected);
        Assert.True(outcome.Hit);
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
    public void DefenderShopOpen_IsRejected()
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 1, defensePower: 200);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0), defenderPshopOpen: true);
        Assert.True(outcome.Rejected);
        Assert.Equal(AttackRejectReason.DefenderShopOpen, outcome.RejectReason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    public void DefenderActionStateBlocksTargeting_IsRejected(int defenderActionSort)
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 1, defensePower: 200);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0), defenderActionSort: defenderActionSort);
        Assert.True(outcome.Rejected);
        Assert.Equal(AttackRejectReason.DefenderActionStateBlocksTargeting, outcome.RejectReason);
    }

    [Fact]
    public void OutOfRange_IsRejected()
    {
        var attacker = Combatant(1, 0, x: 0);
        var defender = Combatant(2, 1, x: 300);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0));
        Assert.Equal(AttackRejectReason.OutOfRange, outcome.RejectReason);
    }

    [Fact]
    public void DefenderWithinZoneEntryGracePeriod_IsRejected()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1, zoneEntryAt: TimeSpan.FromSeconds(5));
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2),
            TimeSpan.FromSeconds(6), null, new ScriptedRandomSource(0));
        Assert.Equal(AttackRejectReason.DefenderProtected, outcome.RejectReason);
    }

    [Fact]
    public void BothSidesPastTheirGracePeriod_AttackProceedsNormally()
    {
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
        var defender = Combatant(2, 1, attackBlock: 500);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(50));
        Assert.False(outcome.Rejected);
        Assert.False(outcome.Hit);
        Assert.Equal(0, outcome.DamageApplied);
    }

    [Fact]
    public void NoBlock_AlwaysHits_NoRollConsumed()
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 1, defensePower: 200, attackBlock: 0);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        Assert.True(outcome.Hit);
        Assert.Equal(160, outcome.DamageApplied);
    }

    [Fact]
    public void Damage_IsAtkMinusDefWithVarianceThenDividedByFive_PvpOnlyQuirk()
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 1, defensePower: 200);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        Assert.Equal(160, outcome.DamageApplied);
    }

    [Fact]
    public void Damage_BelowFloor_ClampsToMinimumBeforeDivision()
    {
        var attacker = Combatant(1, 0, 105);
        var defender = Combatant(2, 1, defensePower: 100);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        Assert.Equal(1, outcome.DamageApplied);
    }

    [Fact]
    public void Charge_IsAppliedBeforeVarianceAndConsumed()
    {
        var attacker = Combatant(1, 0, 1000, chargeBuffPercent: 50);
        var defender = Combatant(2, 1, defensePower: 200);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        Assert.Equal(240, outcome.DamageApplied);
        Assert.True(outcome.ChargeConsumed);
    }

    [Fact]
    public void Charge_IsConsumedOnAMissToo_NotOnlyOnAHit()
    {
        var attacker = Combatant(1, 0, 1000, attackSuccess: 1, chargeBuffPercent: 50);
        var defender = Combatant(2, 1, defensePower: 200, attackBlock: 100_000);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(50));
        Assert.False(outcome.Hit);
        Assert.True(outcome.ChargeConsumed);
    }

    [Fact]
    public void Critical_DoublesDamageBeforeTheFinalDivision()
    {
        var attacker = Combatant(1, 0, 1000, critical: 100);
        var defender = Combatant(2, 1, defensePower: 200, criticalDefence: 0);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0, 50));
        Assert.True(outcome.Critical);
        Assert.Equal(320, outcome.DamageApplied);
    }

    [Fact]
    public void ElementDamage_AddsAfterTheFinalDivision_AndIsNotItselfDivided()
    {
        var attacker = Combatant(1, 0, 1000, elementAttack: 300);
        var defender = Combatant(2, 1, defensePower: 200, elementDefense: 100);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
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

    [Fact]
    public void OverkillBlow_ViewDamageIsFullHit_RealDamageIsCappedToLife()
    {
        var attacker = Combatant(1, 0, 1_000_000);
        var defender = Combatant(2, 1, defensePower: 0, life: 50);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        Assert.Equal(200_000, outcome.ViewDamage);
        Assert.Equal(50, outcome.DamageApplied);
    }

    [Fact]
    public void NonLethalBlow_ViewDamageEqualsRealDamage()
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 1, defensePower: 200);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        Assert.Equal(160, outcome.DamageApplied);
        Assert.Equal(160, outcome.ViewDamage);
    }

    private static AttackForProtocol DuelRequest(int attackerId, int defenderId)
    {
        return new AttackForProtocol
        {
            Case = 1,
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
    public void Duel_SameCharacter_IsRejected()
    {
        var a = Combatant(1, 0);
        var outcome = CombatResolver.ResolveDuelAttack(a, a, DuelRequest(1, 1), TimeSpan.Zero, null,
            new ScriptedRandomSource(0), true, false, 2);
        Assert.True(outcome.Rejected);
        Assert.Equal(AttackRejectReason.SameCharacter, outcome.RejectReason);
    }

    [Fact]
    public void Duel_AttackerDead_IsRejected()
    {
        var attacker = Combatant(1, 0, isDead: true);
        var defender = Combatant(2, 0);
        var outcome = CombatResolver.ResolveDuelAttack(attacker, defender, DuelRequest(1, 2), TimeSpan.Zero, null,
            new ScriptedRandomSource(0), true, false, 2);
        Assert.Equal(AttackRejectReason.AttackerDead, outcome.RejectReason);
    }

    [Fact]
    public void Duel_DefenderDead_IsRejected()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 0, isDead: true);
        var outcome = CombatResolver.ResolveDuelAttack(attacker, defender, DuelRequest(1, 2), TimeSpan.Zero, null,
            new ScriptedRandomSource(0), true, false, 2);
        Assert.Equal(AttackRejectReason.DefenderDead, outcome.RejectReason);
    }

    [Fact]
    public void Duel_DefenderShopOpen_IsRejected()
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 0, defensePower: 200);
        var outcome = CombatResolver.ResolveDuelAttack(attacker, defender, DuelRequest(1, 2), TimeSpan.Zero, null,
            new ScriptedRandomSource(0, 0), true, true, 2);
        Assert.True(outcome.Rejected);
        Assert.Equal(AttackRejectReason.DefenderShopOpen, outcome.RejectReason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    public void Duel_DefenderActionStateBlocksTargeting_IsRejected(int defenderActionSort)
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 0, defensePower: 200);
        var outcome = CombatResolver.ResolveDuelAttack(attacker, defender, DuelRequest(1, 2), TimeSpan.Zero, null,
            new ScriptedRandomSource(0, 0), true, false, defenderActionSort);
        Assert.True(outcome.Rejected);
        Assert.Equal(AttackRejectReason.DefenderActionStateBlocksTargeting, outcome.RejectReason);
    }

    [Fact]
    public void Duel_NotSharingActiveDuel_IsRejected()
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 0, defensePower: 200);
        var outcome = CombatResolver.ResolveDuelAttack(attacker, defender, DuelRequest(1, 2), TimeSpan.Zero, null,
            new ScriptedRandomSource(0, 0), false, false,
            2);
        Assert.True(outcome.Rejected);
        Assert.Equal(AttackRejectReason.DuelNotAuthorized, outcome.RejectReason);
    }

    [Fact]
    public void Duel_SameTribe_IsAllowed_UnlikeEnemyTribeAttack()
    {
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 0, defensePower: 200);
        var outcome = CombatResolver.ResolveDuelAttack(attacker, defender, DuelRequest(1, 2), TimeSpan.Zero, null,
            new ScriptedRandomSource(0, 0), true, false, 2);
        Assert.False(outcome.Rejected);
        Assert.True(outcome.Hit);
        Assert.Equal(160, outcome.DamageApplied);
    }

    [Fact]
    public void Duel_OutOfRange_IsRejected()
    {
        var attacker = Combatant(1, 0, x: 0);
        var defender = Combatant(2, 0, x: 300);
        var outcome = CombatResolver.ResolveDuelAttack(attacker, defender, DuelRequest(1, 2), TimeSpan.Zero, null,
            new ScriptedRandomSource(0), true, false, 2);
        Assert.Equal(AttackRejectReason.OutOfRange, outcome.RejectReason);
    }

    [Fact]
    public void Duel_DamageNeverExceedsDefenderRemainingLife()
    {
        var attacker = Combatant(1, 0, 1_000_000);
        var defender = Combatant(2, 0, defensePower: 0, life: 50);
        var outcome = CombatResolver.ResolveDuelAttack(attacker, defender, DuelRequest(1, 2), TimeSpan.Zero, null,
            new ScriptedRandomSource(0, 0), true, false, 2);
        Assert.False(outcome.Rejected);
        Assert.Equal(50, outcome.DamageApplied);
    }

    [Fact]
    public void Duel_OverkillBlow_ViewDamageIsFullHit_RealDamageIsCappedToLife()
    {
        var attacker = Combatant(1, 0, 1_000_000);
        var defender = Combatant(2, 0, defensePower: 0, life: 50);
        var outcome = CombatResolver.ResolveDuelAttack(attacker, defender, DuelRequest(1, 2), TimeSpan.Zero, null,
            new ScriptedRandomSource(0, 0), true, false, 2);
        Assert.Equal(200_000, outcome.ViewDamage);
        Assert.Equal(50, outcome.DamageApplied);
    }
}
