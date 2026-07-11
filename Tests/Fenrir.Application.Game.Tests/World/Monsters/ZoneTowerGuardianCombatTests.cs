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
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class ZoneTowerGuardianCombatTests
{
    private const int TowerIndex = 0;

    private const short TowerZoneNumber = 2;

    private static readonly EffectiveStats StrongAttacker = new(1000, 1000, 500, 0, 1000, 0, 0, 0, 0, 0, 0);

    private static MonsterEntity CreateGuardian(int towerIndex = TowerIndex, int monsterDefensePower = 0,
        int life = 100_000)
    {
        var template = WorldDataTestRows.Monster(9000) with { Life = life, DefensePower = monsterDefensePower };
        var guardianIndex = TowerWarState.GuardianServerIndex(towerIndex);
        var guardian = MonsterEntity.Create(guardianIndex, 777u, template, guardianIndex, 100, 0, 100, 300f);

        guardian.AiState = MonsterAiState.Decision;
        return guardian;
    }

    private static (Zone Zone, TowerWarState TowerWar) CreateZoneWithActiveGuardian(byte attackerTribe,
        WorldStateService? worldState = null, int towerIndex = TowerIndex)
    {
        var towerWar = new TowerWarState();
        towerWar.SetTowerState(towerIndex, 201, true);

        var zone = ZoneTestKit.CreateZone(TowerZoneNumber, randomSource: new ScriptedAlwaysHitRandomSource(),
            towerWar: towerWar, worldState: worldState);

        zone.SpawnMonster(CreateGuardian(towerIndex));

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10,
            ZoneTestKit.EnterData(session, TowerZoneNumber, "Attacker", tribe: attackerTribe)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetPlayer(10, out var attacker));
        attacker!.Stats = StrongAttacker;

        attacker.AttackSubPacketCeiling = int.MaxValue;

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        return (zone, towerWar);
    }

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
        var (zone, _) = CreateZoneWithActiveGuardian(0);
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
        worldState.SetAllianceOffer(0, 2, true);

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
        var towerWar = new TowerWarState();
        var zone = ZoneTestKit.CreateZone(TowerZoneNumber, randomSource: new ScriptedAlwaysHitRandomSource(),
            towerWar: towerWar);
        var guardian = CreateGuardian();
        zone.SpawnMonster(guardian);

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, TowerZoneNumber, "Attacker")));
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetPlayer(10, out var attacker));
        attacker!.Stats = StrongAttacker;
        attacker.AttackSubPacketCeiling = int.MaxValue;
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 10, AttackInfo = MeleeAgainst(guardian) });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(TowerWarState.GuardianServerIndex(TowerIndex), out var stillAlive));
        Assert.Equal(guardian.Life, stillAlive!.Life);
    }

    [Fact]
    public void NonTowerZone_MonsterAtTheGuardianReservedIndex_IsUnaffectedByTheGate()
    {
        var towerWar = new TowerWarState();
        var zone = ZoneTestKit.CreateZone(39, randomSource: new ScriptedAlwaysHitRandomSource(), towerWar: towerWar);
        var guardianIndex = TowerWarState.GuardianServerIndex(TowerIndex);
        var monster = MonsterEntity.Create(guardianIndex, 1u, WorldDataTestRows.Monster(9001) with { Life = 1000 },
            guardianIndex, 100, 0, 100, 300f);
        monster.AiState = MonsterAiState.Decision;
        zone.SpawnMonster(monster);

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 39, "Attacker", tribe: 0)));
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetPlayer(10, out var attacker));
        attacker!.Stats = StrongAttacker;
        attacker.AttackSubPacketCeiling = int.MaxValue;
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 10, AttackInfo = MeleeAgainst(monster) });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(TowerWarState.GuardianServerIndex(TowerIndex), out var damaged));
        Assert.True(damaged!.Life < 1000);
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
        ZoneTestKit.DrainOutbound(bystanderPipe);

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 10, AttackInfo = MeleeAgainst(guardian!) });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.NotNull(towerWar.GetFirstAttackAtUtc(TowerIndex));
        var bytes = ZoneTestKit.DrainOutbound(bystanderPipe);
        Assert.NotEmpty(bytes);
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

    private sealed class ScriptedAlwaysHitRandomSource : IRandomSource
    {
        public int NextInt32(int exclusiveUpperBound)
        {
            return 0;
        }
    }
}
