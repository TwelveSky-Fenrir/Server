using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

public class ZonePvpKillPopupEventWiringTests
{
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

    private static (Zone Zone, PopupEventRewardSystem PopupSystem) SetUpZone(short mapId,
        params PopupEventType[] enabledPopupTypes)
    {
        var flags = new PopupEventState();
        foreach (var type in enabledPopupTypes)
            flags.SetEnabled(type, true);
        var popupSystem = new PopupEventRewardSystem(flags);

        var zone = ZoneTestKit.CreateZone(mapId, randomSource: new ScriptedRandomSource(0, 0),
            simulationSystems: [popupSystem]);

        var (attackerSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1,
            ZoneTestKit.EnterData(attackerSession, mapId, "Attacker", tribe: 0, level: 145)));

        var (defenderSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(2,
            ZoneTestKit.EnterData(defenderSession, mapId, "Defender", tribe: 1, level: 145)));

        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        attacker!.Stats = StrongAttacker;
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Stats = WeakDefender;
        defender.ActionSort = 1;
        attacker.AttackSubPacketCeiling = int.MaxValue;

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));
        return (zone, popupSystem);
    }

        private static void KillDefender(Zone zone)
    {
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.IsDead = false;
        defender.Life = 1;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.IsDead);
    }

    [Fact]
    public void PopupCounter_AdvancesOnEveryKill_EvenWhileTheCpRewardIsGatedByTheAntiFarmCooldown()
    {
        var (zone, _) = SetUpZone(1, PopupEventType.InvasionPvp);

        KillDefender(zone);
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        var cpAfterFirstKill = attacker!.ContributionPoints;
        Assert.True(cpAfterFirstKill > 0);

        for (var i = 0; i < 4; i++)
            KillDefender(zone);

        Assert.Equal(cpAfterFirstKill, attacker.ContributionPoints);

        zone.Tick(TimeSpan.FromMilliseconds(500));
        Assert.Equal(1, attacker.WarPoint);
    }

    [Fact]
    public void NoPopupSystemRegistered_KillStillGrantsOrdinaryRewards_AndDoesNotThrow()
    {
        var zone = ZoneTestKit.CreateZone(1, randomSource: new ScriptedRandomSource(0, 0));

        var (attackerSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(attackerSession, 1, "Attacker", tribe: 0, level: 42)));
        var (defenderSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(defenderSession, 1, "Defender", tribe: 1, level: 42)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        attacker!.Stats = StrongAttacker;
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Stats = WeakDefender;
        defender.ActionSort = 1;
        attacker.AttackSubPacketCeiling = int.MaxValue;
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        KillDefender(zone);

        Assert.True(attacker.ContributionPoints > 0);
    }
}
