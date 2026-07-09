using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers the shared-acquisition/damage-table behavior contract (2026-07): a monster's own proactive
///     target-acquisition scan (<see cref="MonsterAiSystem.TryAcquireTarget" />) now zero-seeds a slot for the
///     acquired candidate in the SAME 50-capacity attacker table <see cref="Zone.TryDamageMonster" />'s
///     landed-damage accrual and kill-credit resolution both use (<see cref="MonsterEntity.RegisterAcquisition" />,
///     <see cref="MonsterEntity.RegisterAttackDamage" />) -- matching legacy's single shared
///     <c>SetAttackInfoWithAvatar</c> array (<c>Server/ts25zone/S07_MyGame05.cpp:1675-1720</c>), rather than the
///     previously-separate, unread proximity-only aggro list.
/// </summary>
public class MonsterAiSystemKillCreditTests
{
    private static WorldDataCache CacheWithOneRegion()
    {
        var monster = WorldDataTestRows.Monster(600) with
        {
            Life = 1000,
            ItemLevel = 1,
            RealLevel = 1,
            SummonTime1 = 9999,
            SummonTime2 = 9999,
            FrameInfo1 = 1,
            FrameInfo3 = 1,
            // Detection (RadiusInfo2) covers the whole map; melee transition (RadiusInfo1) stays small so a
            // freshly-acquired Chase target does not immediately fall through to AttackWindup before the test
            // gets a chance to observe/kill the monster mid-chase.
            RadiusInfo1 = 2,
            RadiusInfo2 = 1000,
            WalkSpeed = 10,
            RunSpeed = 100,
            // Proactive aggro is gated to AttackType in {1,3,6} (SelectAvatarIndexForPossibleAttack, S07_MyGame05.cpp).
            AttackType = 1
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, 600) with
        {
            Number = 1,
            LocationX = 0,
            LocationY = 0,
            LocationZ = 0,
            Radius = 0
        };

        var rows = WorldDataTestRows.MinimalRows() with { Monsters = [monster], MonsterSpawnRegions = [region] };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static Zone CreateZone(WorldDataCache cache)
    {
        var scheduler = new MonsterSpawnScheduler(cache, static () => new ZeroScatterRandom());
        // Detection's own 50% coin flip (SelectAvatarIndexForPossibleAttack, S07_MyGame05.cpp:208-213) is
        // forced to always succeed so acquisition is deterministic.
        var ai = new MonsterAiSystem(new ScriptedRandomSource(0));
        var options = new GameServerOptions { AoiCellSize = 100_000f };
        return ZoneTestKit.CreateZone(1, options, simulationSystems: [scheduler, ai], worldData: cache);
    }

    /// <summary>Ticks until the monster acquires <paramref name="expectedTargetId" /> as its chase target.</summary>
    private static MonsterEntity AcquireTarget(Zone zone, int expectedTargetId)
    {
        for (var i = 0; i < 10; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out var monster));
            if (monster!.AiState == MonsterAiState.Chase && monster.TargetCharacterId == expectedTargetId)
                return monster;
        }

        throw new InvalidOperationException("monster never acquired the expected target");
    }

    [Fact]
    public void AcquiredButNeverHitTarget_IsCreditedOnDeath_WhenDamageBypassesDamageTracking()
    {
        // Edge case from the shared-acquisition/damage-table behavior contract: a monster dies to damage that
        // bypassed damage-tracking (environment/DoT here, modeled as a null-attacker TryDamageMonster call)
        // while it has a live, merely-chased, never-hit target -- legacy can still default-credit that
        // acquired-but-unhit target because the acquisition write-through already seeded a zero-damage,
        // scannable entry for it (S07_MyGame05.cpp:1763-1766, unconditional-accept-first-surviving-entry).
        var zone = CreateZone(CacheWithOneRegion());
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Chased", 10, posZ: 0)));

        var monster = AcquireTarget(zone, 10);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, monster.Life, null, out var died, out _));
        Assert.True(died);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Equal(10, deadMonster!.KillerCharacterId);
    }

    [Fact]
    public void NoAcquiredTarget_YieldsNoCredit_WhenDamageBypassesDamageTracking()
    {
        // Control case: with nobody ever acquired (no nearby candidate at all), the shared table stays empty,
        // so a death that bypasses damage-tracking still yields a fully unattributed kill -- the fix does not
        // fabricate credit out of nothing.
        var zone = CreateZone(CacheWithOneRegion());

        MonsterEntity? monster = null;
        for (var i = 0; i < 5; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out monster));
        }

        Assert.True(zone.TryDamageMonster(monster!.ServerIndex, monster.Life, null, out var died, out _));
        Assert.True(died);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Null(deadMonster!.KillerCharacterId);
    }

    [Fact]
    public void RealDamageDealer_OutranksAcquisitionOnlyEntry_OnKillCredit()
    {
        // The two algorithms stay separate (acquisition is driven here through the real first-found linear
        // scan, while the second character's damage is applied directly through TryDamageMonster, mirroring
        // the FIFO-capped max-damage lookup's own input), but they now write into the same shared table -- a
        // strictly-greater real hit from a second, never-chased character still outranks the first
        // character's zero-damage acquisition-only entry, matching legacy's strictly-greater-replaces rule
        // (SelectAvatarIndexForMaxAttackDamage, S07_MyGame05.cpp:1767-1772).
        var zone = CreateZone(CacheWithOneRegion());
        var (chasedSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(chasedSession, 1, "Chased", 10, posZ: 0)));

        var monster = AcquireTarget(zone, 10);

        var (attackerSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(11, ZoneTestKit.EnterData(attackerSession, 1, "RealAttacker", 500, posZ: 500)));
        zone.Tick(SimulationClock.LegacyTick); // drains the second Enter

        Assert.True(zone.TryGetMonster(1, out monster));
        Assert.True(zone.TryDamageMonster(monster!.ServerIndex, monster.Life, 11, out var died, out _));
        Assert.True(died);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Equal(11, deadMonster!.KillerCharacterId);
    }

    /// <summary>Forces the spawn scheduler's scatter radius to exactly 0 (same helper as <see cref="MonsterAiSystemTests" />).</summary>
    private sealed class ZeroScatterRandom : Random
    {
        public override double NextDouble()
        {
            return 0;
        }
    }
}
