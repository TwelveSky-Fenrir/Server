using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class ZoneMonsterCombatTribeSymbolDamageUpBonusTests
{
    private static readonly EffectiveStats StrongAttacker = new(1000, 1000, 1000, 0, 1000, 0, 0, 0, 0, 0, 0);

    private static WorldDataCache CacheWithOneRegion()
    {
        var monster = WorldDataTestRows.Monster(700) with
        {
            Life = 1_000_000,
            ItemLevel = 1,
            RealLevel = 1,
            GeneralExperience = 10,
            SummonTime1 = 9999,
            SummonTime2 = 9999,
            FrameInfo1 = 1,
            FrameInfo3 = 1,
            AttackBlock = 0,
            DefensePower = 0,
            ElementDefensePower = 0
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, 700) with
        {
            Number = 1,
            LocationX = 100,
            LocationY = 0,
            LocationZ = 100,
            Radius = 0
        };

        var rows = WorldDataTestRows.MinimalRows() with { Monsters = [monster], MonsterSpawnRegions = [region] };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static (Zone Zone, TribeSymbolCombatModifiers Modifiers, WorldStateService WorldState)
        CreateZoneWithSpawnedMonster(Action<WorldStateService> arrangeWorldState)
    {
        var cache = CacheWithOneRegion();
        var scheduler = new MonsterSpawnScheduler(cache);
        var modifiers = new TribeSymbolCombatModifiers();
        var worldState = ZoneTestKit.CreateWorldState();
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache,
            randomSource: new ScriptedRandomSource(0), tribeSymbolCombatModifiers: modifiers);

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Attacker")));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var spawnedMonster));
        spawnedMonster!.AiState = MonsterAiState.Decision;

        Assert.True(zone.TryGetPlayer(10, out var attacker));
        attacker!.Stats = StrongAttacker;
        attacker.Level = 1;
        attacker.AttackSubPacketCeiling = int.MaxValue;

        arrangeWorldState(worldState);

        new TribeSymbolDamageModifierSystem(worldState, modifiers).Simulate(zone, 1);

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        return (zone, modifiers, worldState);
    }

    private static AttackForProtocol MeleeAgainstMonster(int attackerId, MonsterEntity monster)
    {
        return new AttackForProtocol
        {
            Case = 3,
            ServerIndex1 = attackerId,
            UniqueNumber1 = unchecked((uint)attackerId),
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
    public void AttackerTribeOwnsOneOtherSlot_DamageIsIncreasedByOneFlatIncrement()
    {
        var (zone, modifiers, _) = CreateZoneWithSpawnedMonster(worldState =>
            worldState.ResolveTribeSymbol(2, 1));
        Assert.True(zone.TryGetMonster(1, out var monster));
        Assert.Equal(1, modifiers.GetDamageUpBonusIncrementCount(1));
        var startingLife = monster!.Life;

        zone.PostCombatCommand(new CombatCommand
            { AttackerCharacterId = 10, AttackInfo = MeleeAgainstMonster(10, monster) });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var damaged));
        Assert.Equal(startingLife - 1_500, damaged!.Life);
    }

    [Fact]
    public void AttackerTribeHasNoIncrements_DamageIsUnaffected()
    {
        var (zone, modifiers, _) = CreateZoneWithSpawnedMonster(_ => { });
        Assert.True(zone.TryGetMonster(1, out var monster));
        Assert.Equal(0, modifiers.GetDamageUpBonusIncrementCount(1));
        var startingLife = monster!.Life;

        zone.PostCombatCommand(new CombatCommand
            { AttackerCharacterId = 10, AttackInfo = MeleeAgainstMonster(10, monster) });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var damaged));
        Assert.Equal(startingLife - 1_000, damaged!.Life);
    }
}
