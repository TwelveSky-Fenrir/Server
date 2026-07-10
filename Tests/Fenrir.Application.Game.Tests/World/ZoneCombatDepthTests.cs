using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers the B15 combat depth terms wired into <c>Zone.ApplyCombatCommand</c> (mCase 2) via
///     <c>Zone.Combat.DamagePipeline.cs</c>: reflected damage that can kill the attacker, base-slot Holy-Shield
///     absorption and destroyer-roll removal, and the <c>ProcessAttack02</c> RvR "close the fight" gate.
/// </summary>
public class ZoneCombatDepthTests
{
    private const int ReflectBuffSlot = ReflectResolver.ReflectBuffSlot; // 12
    private const int DestroyerBuffSlot = ReflectResolver.DestroyerBuffSlot; // 14
    private const int ShieldBuffSlot = HolyShieldResolver.BaseSlot; // 9

    private static readonly EffectiveStats StrongAttacker =
        new(1000, 1000, 1000, 0, 100, 0, 0, 0, 0, 0, 0);

    private static readonly EffectiveStats WeakDefender =
        new(1000, 1000, 0, 200, 100, 0, 0, 0, 0, 0, 0);

    private static AttackForProtocol MeleeRequest(int attackerId, int defenderId)
    {
        return new AttackForProtocol
        {
            Case = 2,
            ServerIndex1 = attackerId,
            UniqueNumber1 = unchecked((uint)attackerId),
            ServerIndex2 = defenderId,
            UniqueNumber2 = unchecked((uint)defenderId),
            SenderLocation = [100, 0, 100],
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

    private static Zone TwoPlayerZone(int[] rng, out PlayerRuntimeState attacker, out PlayerRuntimeState defender,
        short mapId = 1, RegularWarActiveMapTracker? tracker = null)
    {
        var zone = ZoneTestKit.CreateZone(mapId, randomSource: new ScriptedRandomSource(rng),
            regularWarActiveMapTracker: tracker);
        var (attackerSession, _) = ZoneTestKit.CreateSession(1);
        var (defenderSession, _) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(attackerSession, mapId, "Attacker", tribe: 0)));
        zone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(defenderSession, mapId, "Defender", tribe: 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var a));
        Assert.True(zone.TryGetPlayer(2, out var d));
        attacker = a!;
        defender = d!;
        attacker.Stats = StrongAttacker;
        defender.Stats = WeakDefender;
        defender.ActionSort = 1; // legal already-acting pose -- see ZoneAttackTests.TwoPlayerZone
        attacker.AttackSubPacketCeiling = int.MaxValue; // skip the sub-packet budget for these fixtures

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1)); // past both sides' protect window
        return zone;
    }

    private static void Attack(Zone zone)
    {
        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    /// <summary>
    ///     Reflect fires: the attacker takes 150% of the pre-element main damage (160 -&gt; 240) and dies from
    ///     its own hit, while the defender takes no damage this hit (the whole attack aborts). Reflect draws
    ///     succeed with all-zero rng (probability roll 0 &lt; 150, gate roll 0 == 0).
    /// </summary>
    [Fact]
    public void ReflectedDamage_KillsAttacker_AndSpareTheDefender()
    {
        // variance(2), variance(11), reflect probability(1500), reflect gate(5) -- all zero.
        var zone = TwoPlayerZone([0, 0, 0, 0], out var attacker, out var defender);
        defender.Buffs.Buff[ReflectBuffSlot * 2] = 150; // max active reflect strength
        attacker.Life = 100; // 240 reflected damage is lethal
        var defenderLifeBefore = defender.Life;

        Attack(zone);

        Assert.True(attacker.IsDead); // killed by its own reflected hit
        Assert.Equal(defenderLifeBefore, defender.Life); // defender untouched -- reflect aborts the hit
    }

    [Fact]
    public void ReflectDoesNotFire_WhenGateRollIsNonZero_DefenderTakesNormalDamage()
    {
        // probability roll 0 (< 150) but gate roll 1 (!= 0) -> no reflect; normal 160 damage lands.
        var zone = TwoPlayerZone([0, 0, 0, 1], out var attacker, out var defender);
        defender.Buffs.Buff[ReflectBuffSlot * 2] = 150;
        attacker.Life = 100;
        var defenderLifeBefore = defender.Life;

        Attack(zone);

        Assert.False(attacker.IsDead);
        Assert.Equal(defenderLifeBefore - 160, defender.Life);
    }

    /// <summary>
    ///     Base-slot Holy-Shield absorbs from the pre-element main damage: a 100-value shield against a 160 hit
    ///     absorbs 100, so the defender loses only 60 and the shield is fully cleared.
    /// </summary>
    [Fact]
    public void HolyShield_AbsorbsFromDamage_ThenClearsWhenConsumed()
    {
        var zone = TwoPlayerZone([0, 0], out var attacker, out var defender); // variance only, no reflect/destroyer
        defender.Buffs.Buff[ShieldBuffSlot * 2] = 100;
        defender.Buffs.Buff[ShieldBuffSlot * 2 + 1] = 42; // some remaining duration
        var defenderLifeBefore = defender.Life;

        Attack(zone);

        Assert.Equal(60, defenderLifeBefore - defender.Life); // 160 - 100 absorbed
        Assert.Equal(0, defender.Buffs.Buff[ShieldBuffSlot * 2]); // shield consumed
        Assert.Equal(0, defender.Buffs.Buff[ShieldBuffSlot * 2 + 1]); // duration cleared too
    }

    [Fact]
    public void HolyShield_LargerThanDamage_AbsorbsFully_DefenderTakesNothing()
    {
        var zone = TwoPlayerZone([0, 0], out var attacker, out var defender);
        defender.Buffs.Buff[ShieldBuffSlot * 2] = 1000; // bigger than the 160 hit
        var defenderLifeBefore = defender.Life;

        Attack(zone);

        Assert.Equal(defenderLifeBefore, defender.Life); // all 160 absorbed
        Assert.Equal(840, defender.Buffs.Buff[ShieldBuffSlot * 2]); // 1000 - 160
    }

    /// <summary>
    ///     A successful destroyer roll (attacker slot-14 strength 201, roll 0 &lt; 201) hard-clears the
    ///     defender's Holy-Shield BEFORE absorption, so the shield can no longer absorb and full damage lands.
    /// </summary>
    [Fact]
    public void DestroyerRoll_ClearsDefenderShield_ThenFullDamageLands()
    {
        // variance(2), variance(11), destroyer(1000) -- all zero; no reflect (defender slot 12 empty).
        var zone = TwoPlayerZone([0, 0, 0], out var attacker, out var defender);
        attacker.Buffs.Buff[DestroyerBuffSlot * 2] = 201; // active destroyer strength
        defender.Buffs.Buff[ShieldBuffSlot * 2] = 1000; // would otherwise absorb the whole hit
        var defenderLifeBefore = defender.Life;

        Attack(zone);

        Assert.Equal(0, defender.Buffs.Buff[ShieldBuffSlot * 2]); // shield destroyed
        Assert.Equal(160, defenderLifeBefore - defender.Life); // full damage, nothing absorbed
    }

    /// <summary>
    ///     ProcessAttack02 RvR "close the fight" gate: on a Zone049-type RvR map (146) whose reported phase has
    ///     reached PostWarCleanup (state 4), the cross-tribe attack is aborted with no damage.
    /// </summary>
    [Fact]
    public void RvrCloseFightGate_Closed_AbortsCrossTribeAttack()
    {
        Assert.True(ZonePvpZoneCatalog.AllowsEnemyTribeAttack(146)); // sanity: 146 is normally an open-PvP map

        var tracker = new RegularWarActiveMapTracker();
        tracker.ReportPhase(146, RegularWarPhase.PostWarCleanup); // state >= 4 -> fight closed

        var zone = TwoPlayerZone([0, 0], out _, out var defender, 146, tracker);
        var defenderLifeBefore = defender.Life;

        Attack(zone);

        Assert.Equal(defenderLifeBefore, defender.Life); // gate closed -- no damage
    }

    [Fact]
    public void RvrCloseFightGate_ActivePhase_AttackStillLands()
    {
        var tracker = new RegularWarActiveMapTracker();
        tracker.ReportPhase(146, RegularWarPhase.Active); // state 3 -- battle in progress, fight NOT closed

        var zone = TwoPlayerZone([0, 0], out _, out var defender, 146, tracker);
        var defenderLifeBefore = defender.Life;

        Attack(zone);

        Assert.Equal(defenderLifeBefore - 160, defender.Life); // gate open -- normal damage lands
    }
}
