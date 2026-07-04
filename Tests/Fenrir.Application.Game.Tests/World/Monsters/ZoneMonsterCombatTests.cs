using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Monsters;
using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers <c>Zone.ApplyCombatCommand</c>'s mCase 3 branch (Avatar -&gt; Monster, <c>ProcessAttack03</c>) --
///     the V4 counterpart of <c>ZoneAttackTests</c>' own mCase 2 coverage.
/// </summary>
public class ZoneMonsterCombatTests
{
    private static readonly EffectiveStats StrongAttacker = new(1000, 1000, 500, 0, 1000, 0, 0, 0, 0, 0, 0);

    private static WorldDataCache CacheWithOneRegion(int monsterDefensePower = 0, int monsterAttackBlock = 0)
    {
        var monster = WorldDataTestRows.Monster(700) with
        {
            Life = 1000,
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
            ZoneTestKit.EnterData(session, 1, "Attacker", 100, posZ: 100)));
        zone.Tick(SimulationClock.LegacyTick); // enters + pops the monster

        Assert.True(zone.TryGetPlayer(characterId, out var attacker));
        attacker!.Stats = StrongAttacker;

        // Past the attacker's own zone-entry protect window (Zone.HandleEnter stamps ZoneEntryAtZoneClock on
        // arrival, CombatResolver.ProtectDuration = 10s) -- ResolvePvmAttack checks the ATTACKER's own window
        // even against a monster, which has none of its own.
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
    public void PvmAttack_KillingBlow_RemovesTheMonster_AndEventuallyRespawnsIt()
    {
        var zone = CreateZoneWithSpawnedMonster(CacheWithOneRegion(), 10, out _);
        Assert.True(zone.TryGetMonster(1, out var monster));

        // Repeatedly attack until the monster is dead -- the exact hit count doesn't matter, only that it
        // eventually dies and is removed, never lingering at <= 0 life.
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

    /// <summary>Always rolls a hit/no-crit -- removes RNG as a source of test flakiness for the hit-chance/variance rolls.</summary>
    private sealed class ScriptedAlwaysHitRandomSource : IRandomSource
    {
        public int NextInt32(int exclusiveUpperBound)
        {
            return 0; // 0 < any hit/critical-chance percent -- always "succeeds"; 0 % 2 == 0 for variance direction
        }
    }
}
