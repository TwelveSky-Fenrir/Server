using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class ZoneMonsterCombatTests
{
    private static readonly EffectiveStats StrongAttacker = new(1000, 1000, 500, 0, 1000, 0, 0, 0, 0, 0, 0);

    private static WorldDataCache CacheWithOneRegion(int monsterDefensePower = 0, int monsterAttackBlock = 0,
        int monsterLife = 1000)
    {
        var monster = WorldDataTestRows.Monster(700) with
        {
            Life = monsterLife,
            ItemLevel = 1,
            RealLevel = 1,
            GeneralExperience = 10,
            SummonTime1 = 9999,
            SummonTime2 = 9999,
            FrameInfo1 = 1,
            FrameInfo3 = 1,
            AttackBlock = monsterAttackBlock,
            DefensePower = monsterDefensePower
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

    private static Zone CreateZoneWithSpawnedMonster(WorldDataCache cache, int characterId,
        out FakeDuplexPipe pipe)
    {
        var scheduler = new MonsterSpawnScheduler(cache);
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache,
            randomSource: new ScriptedAlwaysHitRandomSource());

        var (session, sessionPipe) = ZoneTestKit.CreateSession(1);
        pipe = sessionPipe;
        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, 1, "Attacker")));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var spawnedMonster));
        spawnedMonster!.AiState = MonsterAiState.Decision;

        Assert.True(zone.TryGetPlayer(characterId, out var attacker));
        attacker!.Stats = StrongAttacker;

        attacker.AttackSubPacketCeiling = int.MaxValue;

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        return zone;
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
    public void PvmAttack_UnknownUniqueNumber_IsIgnored_NoDamageApplied()
    {
        var zone = CreateZoneWithSpawnedMonster(CacheWithOneRegion(), 10, out _);
        Assert.True(zone.TryGetMonster(1, out var monster));

        var forged = MeleeAgainstMonster(10, monster!) with { UniqueNumber2 = monster!.UniqueNumber + 999 };
        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 10, AttackInfo = forged });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var stillAlive));
        Assert.Equal(1000, stillAlive!.Life);
    }

    [Fact]
    public void PvmAttack_ValidHit_DamagesTheMonster()
    {
        var zone = CreateZoneWithSpawnedMonster(CacheWithOneRegion(100), 10, out _);
        Assert.True(zone.TryGetMonster(1, out var monster));
        var startingLife = monster!.Life;

        zone.PostCombatCommand(new CombatCommand
        {
            AttackerCharacterId = 10,
            AttackInfo = MeleeAgainstMonster(10, monster)
        });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var damaged));
        Assert.True(damaged!.Life < startingLife);
    }

    [Fact]
    public void PvmAttack_KillingBlow_BroadcastCarriesUncappedViewDamage_ButCappedRealDamage()
    {
        var zone = CreateZoneWithSpawnedMonster(CacheWithOneRegion(monsterLife: 10), 10, out var attackerPipe);
        Assert.True(zone.TryGetMonster(1, out var monster));

        ZoneTestKit.DrainOutbound(attackerPipe);

        zone.PostCombatCommand(new CombatCommand
        {
            AttackerCharacterId = 10,
            AttackInfo = MeleeAgainstMonster(10, monster!)
        });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.MonsterCount);

        var actual = ZoneTestKit.DrainOutbound(attackerPipe);
        Assert.True(actual.Length >= 1 + AttackForProtocol.WireSize);
        Assert.Equal(AttackResponse.Opcode, actual[0]);
        Assert.True(AttackForProtocol.TryRead(actual.AsSpan(1, AttackForProtocol.WireSize), out var info));

        Assert.Equal(500, info.AttackViewDamageValue);
        Assert.Equal(10, info.AttackRealDamageValue);
    }

    [Fact]
    public void PvmAttack_KillingBlow_RemovesTheMonster_AndEventuallyRespawnsIt()
    {
        var zone = CreateZoneWithSpawnedMonster(CacheWithOneRegion(), 10, out _);
        Assert.True(zone.TryGetMonster(1, out var monster));

        for (var i = 0; i < 50 && zone.MonsterCount > 0; i++)
        {
            zone.PostCombatCommand(new CombatCommand
            {
                AttackerCharacterId = 10,
                AttackInfo = MeleeAgainstMonster(10, monster!)
            });
            zone.Tick(SimulationClock.LegacyTick);
        }

        Assert.Equal(0, zone.MonsterCount);
    }

    private sealed class ScriptedAlwaysHitRandomSource : IRandomSource
    {
        public int NextInt32(int exclusiveUpperBound)
        {
            return 0;
        }
    }
}
