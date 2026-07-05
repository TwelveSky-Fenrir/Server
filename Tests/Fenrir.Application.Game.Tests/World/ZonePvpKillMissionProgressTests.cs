using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers the C05 anti-farm wiring: <c>Zone.ApplyCombatCommand</c>'s PvP-kill hook into
///     <see cref="KillCooldownTracker" /> and <see cref="PlayerRuntimeState.MissionKillOtherTribe" />. HP-death
///     behavior itself (<see cref="Zone.ApplyDeath" />) is covered by <c>ZoneAttackTests</c>/<c>ZoneDeathTests</c>;
///     this file only asserts the reward-gating layered on top.
/// </summary>
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

    /// <summary>One attacker (tribe 0) plus <paramref name="defenderIds" /> defenders (tribe 1), past both
    /// zone-entry protect windows and combat-ready.</summary>
    private static Zone SetUpZone(int attackerId, params int[] defenderIds)
    {
        var zone = ZoneTestKit.CreateZone(1, randomSource: new ScriptedRandomSource(0, 0));

        var (attackerSession, _) = ZoneTestKit.CreateSession(attackerId);
        zone.Post(ZoneCommand.Enter(attackerId,
            ZoneTestKit.EnterData(attackerSession, 1, "Attacker", tribe: 0)));

        foreach (var defenderId in defenderIds)
        {
            var (defenderSession, _) = ZoneTestKit.CreateSession(defenderId);
            zone.Post(ZoneCommand.Enter(defenderId,
                ZoneTestKit.EnterData(defenderSession, 1, $"Defender{defenderId}", tribe: 1)));
        }

        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(attackerId, out var attacker));
        attacker!.Stats = StrongAttacker;
        foreach (var defenderId in defenderIds)
        {
            Assert.True(zone.TryGetPlayer(defenderId, out var defender));
            defender!.Stats = WeakDefender;
        }

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1)); // past the zone-entry protect window
        return zone;
    }

    [Fact]
    public void LethalPvpKill_IncrementsAttackersMissionKillOtherTribe()
    {
        var zone = SetUpZone(1, 2);
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Life = 1; // one hit will kill regardless of exact damage roll

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

        // auto-revive, then bring the same victim back down to a killing blow again
        zone.Tick(SimulationClock.DeathReviveDelay + TimeSpan.FromSeconds(1));
        Assert.False(defender.IsDead);
        defender.Life = 1;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // the kill itself is never blocked -- only the daily-mission reward for a second grant this soon
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
