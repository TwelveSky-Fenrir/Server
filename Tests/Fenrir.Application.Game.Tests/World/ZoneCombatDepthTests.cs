using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneCombatDepthTests
{
    private const int ReflectBuffSlot = ReflectResolver.ReflectBuffSlot;
    private const int DestroyerBuffSlot = ReflectResolver.DestroyerBuffSlot;
    private const int ShieldBuffSlot = HolyShieldResolver.BaseSlot;

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
        defender.ActionSort = 1;
        attacker.AttackSubPacketCeiling = int.MaxValue;

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));
        return zone;
    }

    private static void Attack(Zone zone)
    {
        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

        [Fact]
    public void ReflectedDamage_KillsAttacker_AndSpareTheDefender()
    {
        var zone = TwoPlayerZone([0, 0, 0, 0], out var attacker, out var defender);
        defender.Buffs.Buff[ReflectBuffSlot * 2] = 150;
        attacker.Life = 100;
        var defenderLifeBefore = defender.Life;

        Attack(zone);

        Assert.True(attacker.IsDead);
        Assert.Equal(defenderLifeBefore, defender.Life);
    }

    [Fact]
    public void ReflectDoesNotFire_WhenGateRollIsNonZero_DefenderTakesNormalDamage()
    {
        var zone = TwoPlayerZone([0, 0, 0, 1], out var attacker, out var defender);
        defender.Buffs.Buff[ReflectBuffSlot * 2] = 150;
        attacker.Life = 100;
        var defenderLifeBefore = defender.Life;

        Attack(zone);

        Assert.False(attacker.IsDead);
        Assert.Equal(defenderLifeBefore - 160, defender.Life);
    }

        [Fact]
    public void HolyShield_AbsorbsFromDamage_ThenClearsWhenConsumed()
    {
        var zone = TwoPlayerZone([0, 0], out var attacker, out var defender);
        defender.Buffs.Buff[ShieldBuffSlot * 2] = 100;
        defender.Buffs.Buff[ShieldBuffSlot * 2 + 1] = 42;
        var defenderLifeBefore = defender.Life;

        Attack(zone);

        Assert.Equal(60, defenderLifeBefore - defender.Life);
        Assert.Equal(0, defender.Buffs.Buff[ShieldBuffSlot * 2]);
        Assert.Equal(0, defender.Buffs.Buff[ShieldBuffSlot * 2 + 1]);
    }

    [Fact]
    public void HolyShield_LargerThanDamage_AbsorbsFully_DefenderTakesNothing()
    {
        var zone = TwoPlayerZone([0, 0], out var attacker, out var defender);
        defender.Buffs.Buff[ShieldBuffSlot * 2] = 1000;
        var defenderLifeBefore = defender.Life;

        Attack(zone);

        Assert.Equal(defenderLifeBefore, defender.Life);
        Assert.Equal(840, defender.Buffs.Buff[ShieldBuffSlot * 2]);
    }

        [Fact]
    public void DestroyerRoll_ClearsDefenderShield_ThenFullDamageLands()
    {
        var zone = TwoPlayerZone([0, 0, 0], out var attacker, out var defender);
        attacker.Buffs.Buff[DestroyerBuffSlot * 2] = 201;
        defender.Buffs.Buff[ShieldBuffSlot * 2] = 1000;
        var defenderLifeBefore = defender.Life;

        Attack(zone);

        Assert.Equal(0, defender.Buffs.Buff[ShieldBuffSlot * 2]);
        Assert.Equal(160, defenderLifeBefore - defender.Life);
    }

        [Fact]
    public void RvrCloseFightGate_Closed_AbortsCrossTribeAttack()
    {
        Assert.True(ZonePvpZoneCatalog.AllowsEnemyTribeAttack(146));

        var tracker = new RegularWarActiveMapTracker();
        tracker.ReportPhase(146, RegularWarPhase.PostWarCleanup);

        var zone = TwoPlayerZone([0, 0], out _, out var defender, 146, tracker);
        var defenderLifeBefore = defender.Life;

        Attack(zone);

        Assert.Equal(defenderLifeBefore, defender.Life);
    }

    [Fact]
    public void RvrCloseFightGate_ActivePhase_AttackStillLands()
    {
        var tracker = new RegularWarActiveMapTracker();
        tracker.ReportPhase(146, RegularWarPhase.Active);

        var zone = TwoPlayerZone([0, 0], out _, out var defender, 146, tracker);
        var defenderLifeBefore = defender.Life;

        Attack(zone);

        Assert.Equal(defenderLifeBefore - 160, defender.Life);
    }
}
