using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Network.Serialization.Packets.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     <c>Zone.ApplyPvmAttack</c>'s tower-guardian sub-branch (legacy <c>mSpecialSortNumber</c> == 10) --
///     <see cref="TowerFriendlyFireGate" />'s authorization gate wired against a real <see cref="Zone" />
///     tick, plus its allowed-hit side effects (siege-flag clear, first-hit bookkeeping, the
///     <see cref="TowerStatusResponse" /> rebroadcast).
/// </summary>
public class ZoneTowerGuardianCombatTests
{
    private const int TowerIndex = 0;

    // TowerZoneIndexTable.GetTowerIndex(2) == 0 -> TowerZoneIndexTable.GetOwningTribe(2) == tribe 0.
    private const short TowerZoneNumber = 2;

    private static readonly EffectiveStats StrongAttacker = new(1000, 1000, 500, 0, 1000, 0, 0, 0, 0, 0, 0);

    private static MonsterEntity CreateGuardian(int towerIndex = TowerIndex, int monsterDefensePower = 0,
        int life = 100_000)
    {
        var template = WorldDataTestRows.Monster(9000) with { Life = life, DefensePower = monsterDefensePower };
        var guardianIndex = TowerWarState.GuardianServerIndex(towerIndex);
        return MonsterEntity.Create(guardianIndex, 777u, template, guardianIndex, 100, 0, 100, 300f);
    }

    private static (Zone Zone, TowerWarState TowerWar) CreateZoneWithActiveGuardian(byte attackerTribe,
        WorldStateService? worldState = null, int towerIndex = TowerIndex)
    {
        var towerWar = new TowerWarState();
        towerWar.SetTowerState(towerIndex, 201, true); // level 2 type 1, valid => TowerSiegePhase.Active

        var zone = ZoneTestKit.CreateZone(TowerZoneNumber, randomSource: new ScriptedAlwaysHitRandomSource(),
            towerWar: towerWar, worldState: worldState);

        zone.SpawnMonster(CreateGuardian(towerIndex));

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, TowerZoneNumber, "Attacker",
            100, 0, 100, tribe: attackerTribe)));
        zone.Tick(SimulationClock.LegacyTick); // enters + pops the guardian into _players/_monsters

        Assert.True(zone.TryGetPlayer(10, out var attacker));
        attacker!.Stats = StrongAttacker;

        // ResolvePvmAttack checks the attacker's own zone-entry protect window, even against a monster.
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        return (zone, towerWar);
    }

    /// <summary>Synchronous-init helper -- kept out of any <c>[Fact]</c> body so xUnit1031 never fires here.</summary>
    private static WorldStateService CreateInitializedWorldState()
    {
        var service = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    private static AttackForProtocol MeleeAgainst(MonsterEntity monster, int attackerCharacterId = 10)
    {
        return new AttackForProtocol
        {
            Case = 3,
            ServerIndex1 = attackerCharacterId,
            UniqueNumber1 = unchecked((uint)attackerCharacterId),
            ServerIndex2 = monster.ServerIndex,
            UniqueNumber2 = monster.UniqueNumber,
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

    [Fact]
    public void AttackFromUnrelatedTribe_TowerActivelyBuilt_DamagesTheGuardian()
    {
        var (zone, _) = CreateZoneWithActiveGuardian(1);
        Assert.True(zone.TryGetMonster(TowerWarState.GuardianServerIndex(TowerIndex), out var guardian));
        var startingLife = guardian!.Life;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 10, AttackInfo = MeleeAgainst(guardian) });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(TowerWarState.GuardianServerIndex(TowerIndex), out var damaged));
        Assert.True(damaged!.Life < startingLife);
    }

    [Fact]
    public void AttackFromOwningTribe_IsRejected_SelfTribeProtection()
    {
        var (zone, _) = CreateZoneWithActiveGuardian(0); // owner of zone 2's tower is tribe 0
        Assert.True(zone.TryGetMonster(TowerWarState.GuardianServerIndex(TowerIndex), out var guardian));
        var startingLife = guardian!.Life;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 10, AttackInfo = MeleeAgainst(guardian) });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(TowerWarState.GuardianServerIndex(TowerIndex), out var stillAlive));
        Assert.Equal(startingLife, stillAlive!.Life);
    }

    [Fact]
    public void AttackFromTribeAlliedWithTheOwner_IsRejected_TheFriendlyFireFix()
    {
        var worldState = CreateInitializedWorldState();
        worldState.SetAllianceOffer(0, 2, true); // tribe 0 (this tower's owner) allied with tribe 2

        var (zone, _) = CreateZoneWithActiveGuardian(2, worldState);
        Assert.True(zone.TryGetMonster(TowerWarState.GuardianServerIndex(TowerIndex), out var guardian));
        var startingLife = guardian!.Life;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 10, AttackInfo = MeleeAgainst(guardian) });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(TowerWarState.GuardianServerIndex(TowerIndex), out var stillAlive));
        Assert.Equal(startingLife, stillAlive!.Life);
    }

    [Fact]
    public void TowerNotActivelyBuilt_AttackIsRejected_EvenFromAnUnrelatedTribe()
    {
        var towerWar = new TowerWarState(); // fresh: Dormant, never built
        var zone = ZoneTestKit.CreateZone(TowerZoneNumber, randomSource: new ScriptedAlwaysHitRandomSource(),
            towerWar: towerWar);
        var guardian = CreateGuardian();
        zone.SpawnMonster(guardian);

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, TowerZoneNumber, "Attacker",
            100, 0, 100, tribe: 1)));
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetPlayer(10, out var attacker));
        attacker!.Stats = StrongAttacker;
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 10, AttackInfo = MeleeAgainst(guardian) });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(TowerWarState.GuardianServerIndex(TowerIndex), out var stillAlive));
        Assert.Equal(guardian.Life, stillAlive!.Life);
    }

    [Fact]
    public void NonTowerZone_MonsterAtTheGuardianReservedIndex_IsUnaffectedByTheGate()
    {
        // Zone 39 has no recognized tower slot at all (GetTowerIndex returns -1), so isTowerGuardian must be
        // false regardless of the monster's own ServerIndex -- the friendly-fire gate must never engage here.
        var towerWar = new TowerWarState();
        var zone = ZoneTestKit.CreateZone(39, randomSource: new ScriptedAlwaysHitRandomSource(), towerWar: towerWar);
        var guardianIndex = TowerWarState.GuardianServerIndex(TowerIndex);
        var monster = MonsterEntity.Create(guardianIndex, 1u, WorldDataTestRows.Monster(9001) with { Life = 1000 },
            guardianIndex, 100, 0, 100, 300f);
        zone.SpawnMonster(monster);

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 39, "Attacker",
            100, 0, 100, tribe: 0)));
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetPlayer(10, out var attacker));
        attacker!.Stats = StrongAttacker;
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 10, AttackInfo = MeleeAgainst(monster) });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(TowerWarState.GuardianServerIndex(TowerIndex), out var damaged));
        Assert.True(damaged!.Life < 1000); // attack went through -- the gate never engages outside a tower zone
    }

    [Fact]
    public void AllowedHit_ClearsUnderAttackFlag_AndRefreshesTheLastAttackTimestamp()
    {
        var (zone, towerWar) = CreateZoneWithActiveGuardian(1);
        Assert.True(zone.TryGetMonster(TowerWarState.GuardianServerIndex(TowerIndex), out var guardian));

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 10, AttackInfo = MeleeAgainst(guardian!) });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.False(towerWar.IsUnderAttack(TowerIndex));
        Assert.NotNull(towerWar.GetLastAttackAtUtc(TowerIndex));
    }

    [Fact]
    public void FirstAllowedHit_RecordsTheFirstAttackTimestamp_AndBroadcastsTowerStatusToEveryPlayerInTheZone()
    {
        var (zone, towerWar) = CreateZoneWithActiveGuardian(1);
        Assert.True(zone.TryGetMonster(TowerWarState.GuardianServerIndex(TowerIndex), out var guardian));

        var (bystanderSession, bystanderPipe) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(bystanderSession, TowerZoneNumber, "Bystander",
            500, 0, 500, tribe: 3)));
        zone.Tick(SimulationClock.LegacyTick);
        ZoneTestKit.DrainOutbound(bystanderPipe); // discard the enter/replication noise before asserting

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 10, AttackInfo = MeleeAgainst(guardian!) });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.NotNull(towerWar.GetFirstAttackAtUtc(TowerIndex));
        var bytes = ZoneTestKit.DrainOutbound(bystanderPipe);
        Assert.NotEmpty(bytes); // the full tower-state rebroadcast reached a bystander who never attacked
    }

    [Fact]
    public void SecondAllowedHit_IsNotFirst_AndDoesNotOverwriteTheFirstAttackTimestamp()
    {
        var (zone, towerWar) = CreateZoneWithActiveGuardian(1);
        Assert.True(zone.TryGetMonster(TowerWarState.GuardianServerIndex(TowerIndex), out var guardian));

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 10, AttackInfo = MeleeAgainst(guardian!) });
        zone.Tick(SimulationClock.LegacyTick);
        var firstAttackAt = towerWar.GetFirstAttackAtUtc(TowerIndex);
        Assert.NotNull(firstAttackAt);

        Assert.True(zone.TryGetMonster(TowerWarState.GuardianServerIndex(TowerIndex), out var stillAlive));
        zone.PostCombatCommand(new CombatCommand
            { AttackerCharacterId = 10, AttackInfo = MeleeAgainst(stillAlive!) });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(firstAttackAt, towerWar.GetFirstAttackAtUtc(TowerIndex));
    }

    /// <summary>Always rolls a hit/no-crit -- removes RNG as a source of test flakiness for the hit-chance/variance rolls.</summary>
    private sealed class ScriptedAlwaysHitRandomSource : IRandomSource
    {
        public int NextInt32(int exclusiveUpperBound)
        {
            return 0;
        }
    }
}
