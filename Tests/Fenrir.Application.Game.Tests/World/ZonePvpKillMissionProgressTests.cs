using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

public class ZonePvpKillMissionProgressTests
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

        private static Zone SetUpZone(int attackerId, params int[] defenderIds)
    {
        var worldState = ZoneTestKit.CreateWorldState();
        var zone = ZoneTestKit.CreateZone(49, randomSource: new ScriptedRandomSource(0, 0), worldState: worldState,
            simulationSystems: [new DeathGateTickSystem(worldState)]);

        var (attackerSession, _) = ZoneTestKit.CreateSession(attackerId);
        zone.Post(ZoneCommand.Enter(attackerId,
            ZoneTestKit.EnterData(attackerSession, 49, "Attacker", tribe: 0)));

        foreach (var defenderId in defenderIds)
        {
            var (defenderSession, _) = ZoneTestKit.CreateSession(defenderId);
            zone.Post(ZoneCommand.Enter(defenderId,
                ZoneTestKit.EnterData(defenderSession, 49, $"Defender{defenderId}", tribe: 1)));
        }

        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(attackerId, out var attacker));
        attacker!.Stats = StrongAttacker;
        foreach (var defenderId in defenderIds)
        {
            Assert.True(zone.TryGetPlayer(defenderId, out var defender));
            defender!.Stats = WeakDefender;
            defender.ActionSort = 1;
        }

        attacker.AttackSubPacketCeiling = int.MaxValue;

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));
        return zone;
    }

    [Fact]
    public void LethalPvpKill_IncrementsAttackersMissionKillOtherTribe()
    {
        var zone = SetUpZone(1, 2);
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Life = 1;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.IsDead);
        Assert.Equal(1, attacker!.MissionKillOtherTribe);
    }

    [Fact]
    public void NonLethalHit_GrantsNoMissionProgress()
    {
        var zone = SetUpZone(1, 2);
        Assert.True(zone.TryGetPlayer(1, out var attacker));

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, attacker!.MissionKillOtherTribe);
    }

    [Fact]
    public void RepeatKillOfSameVictim_WithinCooldown_StillKillsButDoesNotDoubleGrant()
    {
        var zone = SetUpZone(1, 2);
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Life = 1;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(1, attacker!.MissionKillOtherTribe);
        Assert.True(defender.IsDead);

        zone.Tick(SimulationClock.ReviveEligibilityDelay + TimeSpan.FromSeconds(1));
        Assert.False(defender.IsDead);
        defender.Life = 1;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.IsDead);
        Assert.Equal(1, attacker.MissionKillOtherTribe);
    }

    [Fact]
    public void KillingADifferentVictim_IsNotGatedByAnotherPairsCooldown()
    {
        var zone = SetUpZone(1, 2, 3);
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defenderA));
        Assert.True(zone.TryGetPlayer(3, out var defenderB));
        defenderA!.Life = 1;
        defenderB!.Life = 1;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(1, attacker!.MissionKillOtherTribe);

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 3) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(2, attacker.MissionKillOtherTribe);
    }

    [Fact]
    public void MissionKillOtherTribe_ClampsAtTheLegacyCap()
    {
        var zone = SetUpZone(1, 2);
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        attacker!.MissionKillOtherTribe = KillCooldownTracker.MissionKillOtherTribeCap;
        defender!.Life = 1;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(KillCooldownTracker.MissionKillOtherTribeCap, attacker.MissionKillOtherTribe);
    }
}
