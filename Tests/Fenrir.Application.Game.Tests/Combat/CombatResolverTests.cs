using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

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
        // zoneAllowsEnemyTribeAttack defaults to true -- a normal field/PvP zone's flag (legacy value 1 or 2,
        // both equally "enabled" per S18_MyZoneInfo.cpp table entries and the equals-zero-only test).
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
        // Legacy zone-gate value 0 (e.g. an unlisted zone id, defaulted to disabled -- S18_MyZoneInfo.cpp:15-18)
        // rejects silently before the same-tribe/alliance check even runs (S07_MyGame02.cpp:945-950).
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
        // pvp-flagging-safezone-rules (Critical): zone 324/FFAMAPNUM 335 skip the same-tribe/allied-tribe
        // rejection entirely (S07_MyGame02.cpp:952-958) -- caller passes sameTribeAttackExempt: true, resolved
        // via ZonePvpZoneCatalog.IsSameTribeAttackExempt for its own zone id.
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
        // tribe-previoustribe-runtime-gating (Major): AttackPlayer's ENEMY-branch gate rejects not only
        // same-tribe but also a defender whose tribe is the tribe currently allied with the attacker's own
        // (S07_MyGame02.cpp:954) -- allyOfAttackerTribe is the caller-resolved live RvR alliance fact
        // (WorldStateService.GetAllyOf against the ATTACKER's own tribe).
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1); // defender's tribe (1) is the attacker's tribe's (0) current ally
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0), allyOfAttackerTribe: 1);
        Assert.True(outcome.Rejected);
        Assert.Equal(AttackRejectReason.SameOrAlliedTribe, outcome.RejectReason);
    }

    [Fact]
    public void DifferentTribe_NotTheCurrentAlly_IsNotRejectedByTheAllianceHalf()
    {
        // The allied-tribe half must only match the SPECIFIC tribe returned by the live alliance lookup, not
        // any non-attacker tribe -- a third, unallied tribe must still be attackable.
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 2, defensePower: 200); // attacker's ally is tribe 1, not tribe 2
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0), allyOfAttackerTribe: 1);
        Assert.False(outcome.Rejected);
        Assert.True(outcome.Hit);
    }

    [Fact]
    public void AlliedTribe_OnOpenPvpMap_IsExemptFromRejection()
    {
        // Same FFA exemption as the same-tribe half -- zone 324/FFAMAPNUM 335 skip the ENTIRE tribe-identity
        // comparison, both halves (S07_MyGame02.cpp:952-958).
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
        // allyOfAttackerTribe defaults to null (no active alliance) -- only the same-tribe half can reject.
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
        // pvp-flagging-safezone-rules (Major): home-tribe district sub-zones 2/3/4/7/8/9/12/13/14 gate an
        // attacker whose level is >= 90 from attacking a defender whose level is < 90
        // (S07_MyGame02.cpp:960-976). Caller passes newbieProtectionZone: true, resolved via
        // ZonePvpZoneCatalog.IsNewbieProtectionZone for its own zone id.
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
        // Protection only applies while the DEFENDER is itself under 90 -- an equal-or-higher-level defender
        // does not qualify (S07_MyGame02.cpp:971, defenser->mDATA.aLevel1 < 90 is the second half of the &&).
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
        // newbieProtectionZone defaults to false -- e.g. the three "capital plaza" zones (1, 6, 11) are
        // conspicuously absent from the legacy switch's case list, so full enemy-tribe PvP applies there
        // regardless of level (S07_MyGame02.cpp:960-976).
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
        // pvp-flagging-safezone-rules (Major): AttackPlayer's shared shop-open precondition
        // (S07_MyGame02.cpp:917-920) -- same gate ResolveDuelAttack already reproduces (Duel_DefenderShopOpen_IsRejected).
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 1, defensePower: 200);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0), defenderPshopOpen: true);
        Assert.True(outcome.Rejected);
        Assert.Equal(AttackRejectReason.DefenderShopOpen, outcome.RejectReason);
    }

    [Theory]
    [InlineData(0)] // "no action yet" placeholder
    [InlineData(12)] // death pose
    public void DefenderActionStateBlocksTargeting_IsRejected(int defenderActionSort)
    {
        // pvp-flagging-safezone-rules (Major): CheckPossibleAttackTarget's avatar-target rule
        // (S07_MyGame02.cpp:921-924, :1692-1703) -- same gate ResolveDuelAttack already reproduces
        // (Duel_DefenderActionStateBlocksTargeting_IsRejected).
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

    [Fact]
    public void OverkillBlow_ViewDamageIsFullHit_RealDamageIsCappedToLife()
    {
        // S07_MyGame02.cpp:1361-1366 -- mAttackViewDamageValue (the floating damage number the client shows)
        // is captured BEFORE the life-cap clamp; mAttackRealDamageValue (life actually lost) AFTER it. On a
        // killing/overkill blow the two MUST diverge: the client still displays the full hit size.
        var attacker = Combatant(1, 0, 1_000_000);
        var defender = Combatant(2, 1, defensePower: 0, life: 50);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        // (1_000_000-0) -> variance no-op -> no crit -> /5 = 200_000 (the full "view" hit), capped to 50 life.
        Assert.Equal(200_000, outcome.ViewDamage);
        Assert.Equal(50, outcome.DamageApplied);
    }

    [Fact]
    public void NonLethalBlow_ViewDamageEqualsRealDamage()
    {
        // The two numbers coincide whenever the computed hit is at most the defender's remaining life.
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 1, defensePower: 200);
        var outcome = CombatResolver.ResolveEnemyTribeAttack(attacker, defender, MeleeRequest(1, 2), TimeSpan.Zero,
            null, new ScriptedRandomSource(0, 0));
        // (1000-200)/5 = 160, far below the defender's 100_000 life -> no clamp -> view == real.
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
    [InlineData(0)] // "no action yet" placeholder
    [InlineData(12)] // death pose
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
        // Both flagged "attackable" (non-0/12 action sort, shop closed) but the caller-resolved duel-pairing
        // fact says false -- the duel-specific authorization gate (S07_MyGame02.cpp:935-943).
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
        // Duels never evaluate ResolveEnemyTribeAttack's own same-tribe/alliance gate -- two same-tribe
        // characters sharing an active duel must be able to damage each other.
        var attacker = Combatant(1, 0, 1000);
        var defender = Combatant(2, 0, defensePower: 200);
        var outcome = CombatResolver.ResolveDuelAttack(attacker, defender, DuelRequest(1, 2), TimeSpan.Zero, null,
            new ScriptedRandomSource(0, 0), true, false, 2);
        Assert.False(outcome.Rejected);
        Assert.True(outcome.Hit);
        // Same shared ResolveDamage formula as mCase 2: (1000-200)=800 -> variance no-op -> no crit -> /5 = 160.
        Assert.Equal(160, outcome.DamageApplied);
    }

    [Fact]
    public void Duel_OutOfRange_IsRejected()
    {
        var attacker = Combatant(1, 0, x: 0);
        var defender = Combatant(2, 0, x: 300); // > 185 max attack distance
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
        // Duel shares AttackPlayer's view-before-clamp / real-after-clamp split (S07_MyGame02.cpp:1361-1366).
        var attacker = Combatant(1, 0, 1_000_000);
        var defender = Combatant(2, 0, defensePower: 0, life: 50);
        var outcome = CombatResolver.ResolveDuelAttack(attacker, defender, DuelRequest(1, 2), TimeSpan.Zero, null,
            new ScriptedRandomSource(0, 0), true, false, 2);
        Assert.Equal(200_000, outcome.ViewDamage);
        Assert.Equal(50, outcome.DamageApplied);
    }
}
