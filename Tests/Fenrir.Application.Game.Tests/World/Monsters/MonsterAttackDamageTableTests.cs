using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterAttackDamageTableTests
{
    private static Zone CreateZoneWithManualMonster(int serverIndex, int life, out MonsterEntity monster)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var template = WorldDataTestRows.Monster(600) with { Life = life };
        monster = MonsterEntity.Create(serverIndex, (uint)serverIndex, template, 1,
            0, 0, 0, 50);
        zone.SpawnMonster(monster);
        return zone;
    }

    private static void EnterCharacter(Zone zone, int characterId, string name)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, 1, name)));
    }

    [Fact]
    public void RegisterAttackDamage_AccruesAdditively_AcrossMultipleHitsFromSameAttacker()
    {
        var zone = CreateZoneWithManualMonster(1, 1000, out var monster);
        EnterCharacter(zone, 10, "A");
        EnterCharacter(zone, 11, "B");
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 5, 10, out var died1, out _));
        Assert.False(died1);
        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 5, 10, out var died2, out _));
        Assert.False(died2);
        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 8, 11, out var died3, out var remaining));
        Assert.False(died3);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, remaining, null, out var died4, out _));
        Assert.True(died4);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Equal(10, deadMonster!.KillerCharacterId);
    }

    [Fact]
    public void RegisterAcquisition_ReAcquisitionOfAlreadyTrackedTarget_DoesNotResetAccumulatedRealDamage()
    {
        var monsterRow = WorldDataTestRows.Monster(601) with
        {
            Life = 100_000,
            ItemLevel = 1,
            RealLevel = 1,
            SummonTime1 = 9999,
            SummonTime2 = 9999,
            FrameInfo1 = 1,
            FrameInfo3 = 1,
            RadiusInfo1 = 2,
            RadiusInfo2 = 1000,
            WalkSpeed = 10,
            RunSpeed = 100,
            AttackType = 1
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, 601) with
        {
            Number = 1, LocationX = 0, LocationY = 0, LocationZ = 0, Radius = 0
        };
        var rows = WorldDataTestRows.MinimalRows() with { Monsters = [monsterRow], MonsterSpawnRegions = [region] };
        var cache = WorldDataCacheBuilder.Build(rows).Cache;

        var scheduler = new MonsterSpawnScheduler(cache, static () => new ZeroScatterRandom());
        var ai = new MonsterAiSystem(new ScriptedRandomSource(0));
        var options = new GameServerOptions { AoiCellSize = 100_000f };
        var zone = ZoneTestKit.CreateZone(1, options, simulationSystems: [scheduler, ai], worldData: cache);

        EnterCharacter(zone, 10, "A");
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(1, out var monster));
        Assert.Equal(MonsterAiState.Decision, monster!.AiState);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 50, 10, out var diedFromRealHit, out _));
        Assert.False(diedFromRealHit);

        for (var i = 0; i < 10 && monster!.AiState != MonsterAiState.Chase; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out monster));
        }

        Assert.Equal(MonsterAiState.Chase, monster!.AiState);
        Assert.Equal(10, monster.TargetCharacterId);

        EnterCharacter(zone, 11, "B");
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 20, 11, out var diedFromB, out _));
        Assert.False(diedFromB);

        Assert.True(zone.TryGetMonster(1, out monster));
        Assert.True(zone.TryDamageMonster(monster!.ServerIndex, monster.Life, null, out var died, out _));
        Assert.True(died);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Equal(10, deadMonster!.KillerCharacterId);
    }

    [Fact]
    public void FiftyFirstAttacker_EvictsOldestSlot_NotLowestDamageSlot()
    {
        const int firstAttackerId = 1000;
        const int fiftyFirstAttackerId = firstAttackerId + 50;

        var zone = CreateZoneWithManualMonster(1, 1_000_000, out var monster);

        for (var i = 0; i < 51; i++)
            EnterCharacter(zone, firstAttackerId + i, $"Attacker{i}");
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 5000, firstAttackerId, out var died1, out _));
        Assert.False(died1);

        for (var i = 1; i <= 49; i++)
        {
            Assert.True(zone.TryDamageMonster(monster.ServerIndex, 1, firstAttackerId + i, out var diedFromFill,
                out _));
            Assert.False(diedFromFill);
        }

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 10, fiftyFirstAttackerId, out var died51,
            out var remaining));
        Assert.False(died51);

        Assert.True(zone.TryGetMonster(1, out monster));
        Assert.True(zone.TryDamageMonster(monster!.ServerIndex, remaining, null, out var died, out _));
        Assert.True(died);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Equal(fiftyFirstAttackerId, deadMonster!.KillerCharacterId);
    }

    private sealed class ZeroScatterRandom : Random
    {
        public override double NextDouble()
        {
            return 0;
        }
    }
}
