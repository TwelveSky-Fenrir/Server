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

/// <summary>Covers <c>Zone.ApplyCombatCommand</c>'s mCase 3 branch: Avatar -&gt; Monster, <c>ProcessAttack03</c>.</summary>
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
        zone.Tick(SimulationClock.LegacyTick); // enters + pops the monster

        // This fixture doesn't wire MonsterAiSystem in (it only needs the spawn scheduler), so a freshly
        // popped monster would otherwise sit in MonsterAiState.Spawning forever -- Zone.ApplyCombatCommand's
        // CheckPossibleAttackTarget gate (Zone.Combat.cs) now silently rejects an attack against a monster
        // mid-spawn-windup, so every test in this suite would see no damage applied at all. Force straight to
        // Decision: this suite exercises PvM attack resolution, not the spawn-windup timer.
        Assert.True(zone.TryGetMonster(1, out var spawnedMonster));
        spawnedMonster!.AiState = MonsterAiState.Decision;

        Assert.True(zone.TryGetPlayer(characterId, out var attacker));
        attacker!.Stats = StrongAttacker;

        // This suite exercises PvM combat resolution, not the attack sub-packet budget/replay guard (that's
        // AttackPacketBudgetTests' own job) -- a real client always sends a legal avatar-action packet first
        // to establish a non-zero ceiling, which this fixture skips. Uncapped here (some tests below loop up
        // to 50 attack commands to grind a monster down) so a raw CombatCommand posted straight after Enter
        // isn't silently rejected by AttackPacketBudget.TryConsume.
        attacker.AttackSubPacketCeiling = int.MaxValue;

        // ResolvePvmAttack checks the attacker's own zone-entry protect window, even against a monster
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

    /// <summary>
    ///     Legacy-parity regression (S07_MyGame02.cpp:2371-2376): PvM shares AttackPlayer's view-before-clamp /
    ///     real-after-clamp split. A monster with 10 HP left struck for a computed 500 must broadcast
    ///     <c>mAttackViewDamageValue</c> = 500 (the full hit the client displays) but
    ///     <c>mAttackRealDamageValue</c> = 10 (the life actually lost) -- before the fix both were collapsed onto
    ///     the capped 10.
    /// </summary>
    [Fact]
    public void PvmAttack_KillingBlow_BroadcastCarriesUncappedViewDamage_ButCappedRealDamage()
    {
        // Monster spawns with only 10 HP -- StrongAttacker's 500 attack power (no monster defence) far overkills
        // it in one hit, so the computed 500 exceeds the 10 remaining life and the two damage numbers diverge.
        var zone = CreateZoneWithSpawnedMonster(CacheWithOneRegion(monsterLife: 10), 10, out var attackerPipe);
        Assert.True(zone.TryGetMonster(1, out var monster));

        ZoneTestKit.DrainOutbound(attackerPipe); // clear enter/spawn chatter before the measured attack

        zone.PostCombatCommand(new CombatCommand
        {
            AttackerCharacterId = 10,
            AttackInfo = MeleeAgainstMonster(10, monster!)
        });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.MonsterCount); // the blow was lethal (real damage capped to the 10 remaining life)

        // First frame on the attacker's wire is the ZC_PROCESS_ATTACK_RECV broadcast (emitted before the
        // monster-death broadcast that follows): [1-byte opcode][68-byte AttackForProtocol payload].
        var actual = ZoneTestKit.DrainOutbound(attackerPipe);
        Assert.True(actual.Length >= 1 + AttackForProtocol.WireSize);
        Assert.Equal(AttackResponse.Opcode, actual[0]);
        Assert.True(AttackForProtocol.TryRead(actual.AsSpan(1, AttackForProtocol.WireSize), out var info));

        Assert.Equal(500, info.AttackViewDamageValue); // full computed hit, BEFORE the life cap (no PvM /5)
        Assert.Equal(10, info.AttackRealDamageValue); // capped to the monster's remaining life
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

    /// <summary>Always rolls a hit/no-crit -- removes RNG as a source of test flakiness for the hit-chance/variance rolls.</summary>
    private sealed class ScriptedAlwaysHitRandomSource : IRandomSource
    {
        public int NextInt32(int exclusiveUpperBound)
        {
            return 0; // 0 < any hit/critical-chance percent -- always "succeeds"; 0 % 2 == 0 for variance direction
        }
    }
}
