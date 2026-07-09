using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers the shared 50-slot attacker table (<c>MAX_MONSTER_OBJECT_ATTACK_NUM</c>,
///     <c>Server/ts25zone/S07_MyGame05.cpp:1675-1720</c>, <c>MonsterEntity.WriteAttackDamageSlot</c>) that
///     <see cref="MonsterEntity.RegisterAttackDamage" /> (real, positive-damage hits) and
///     <see cref="MonsterEntity.RegisterAcquisition" /> (zero-seeded acquisition write-through) both write
///     into. Unlike <see cref="MonsterAiSystemKillCreditTests" />, which covers the shared-table BEHAVIOR
///     CONTRACT (acquisition-only credit surviving a damage-tracking bypass), this file isolates the two
///     underlying WRITE ALGORITHMS themselves -- additive real-damage accrual, zero-seed-is-a-no-op-on-an-
///     already-tracked-slot re-acquisition -- plus the FIFO-cap-50 eviction rule both algorithms share.
///     <see cref="MonsterEntity.RegisterAttackDamage" />/<see cref="MonsterEntity.RegisterAcquisition" />/
///     <see cref="MonsterEntity.SnapshotAttackDamage" /> are all <c>internal</c> and this test project has no
///     <c>InternalsVisibleTo</c> grant from the Domain assembly, so every assertion here observes the table
///     only through <see cref="Zone" />'s public surface (<see cref="Zone.TryDamageMonster" />,
///     <see cref="Zone.TryDequeueDeadMonster" />, <see cref="Zone.SpawnMonster" />) -- kill-credit outcome is
///     the only externally-visible witness of the table's internal state.
/// </summary>
public class MonsterAttackDamageTableTests
{
    /// <summary>
    ///     A zone with no <see cref="MonsterAiSystem" /> and no spawn scheduler wired in -- these tests drive
    ///     <see cref="Zone.TryDamageMonster" /> directly and never need the monster to actually think, so a
    ///     manually-<see cref="Zone.SpawnMonster" />-ed entity avoids needing a spawn region/world-data catalog
    ///     at all (contrast <see cref="MonsterAiSystemKillCreditTests" />'s <c>CacheWithOneRegion</c>+scheduler
    ///     fixture, needed there only because that file's scenarios go through the real acquisition scan).
    /// </summary>
    private static Zone CreateZoneWithManualMonster(int serverIndex, int life, out MonsterEntity monster)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var template = WorldDataTestRows.Monster(600) with { Life = life };
        monster = MonsterEntity.Create(serverIndex, (uint)serverIndex, template, 1,
            0, 0, 0, 50);
        zone.SpawnMonster(monster);
        return zone;
    }

    /// <summary>Enters one character, draining the Enter command so it's live in the zone's own player map.</summary>
    private static void EnterCharacter(Zone zone, int characterId, string name)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, 1, name)));
    }

    [Fact]
    public void RegisterAttackDamage_AccruesAdditively_AcrossMultipleHitsFromSameAttacker()
    {
        // Isolates RegisterAttackDamage's own algorithm: repeated real hits from the SAME attacker must SUM
        // onto that attacker's one tracked slot (WriteAttackDamageSlot's "existing.CumulativeDamage +=
        // damage" branch), never overwrite/replace the running total with just the latest hit. Attacker A
        // lands two small hits (5 + 5 = 10 total); Attacker B lands one hit worth more than any SINGLE one of
        // A's hits but less than A's true running total (8, between 5 and 10). If accrual were a last-write
        // overwrite instead of a sum, A's tracked value would read back as 5 (its last hit only) and B (8)
        // would incorrectly outrank it -- this discriminates the two implementations.
        var zone = CreateZoneWithManualMonster(1, 1000, out var monster);
        EnterCharacter(zone, 10, "A");
        EnterCharacter(zone, 11, "B");
        zone.Tick(SimulationClock.LegacyTick); // drains both Enters

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 5, 10, out var died1, out _));
        Assert.False(died1);
        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 5, 10, out var died2, out _));
        Assert.False(died2);
        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 8, 11, out var died3, out var remaining));
        Assert.False(died3);

        // Finishing blow bypasses damage-tracking entirely (null attacker) so the kill-credit scan reads back
        // exactly the two entries built above, undisturbed by a third write.
        Assert.True(zone.TryDamageMonster(monster.ServerIndex, remaining, null, out var died4, out _));
        Assert.True(died4);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Equal(10, deadMonster!.KillerCharacterId); // A's true 10 > B's 8
    }

    [Fact]
    public void RegisterAcquisition_ReAcquisitionOfAlreadyTrackedTarget_DoesNotResetAccumulatedRealDamage()
    {
        // Isolates RegisterAcquisition's own algorithm in contrast to RegisterAttackDamage: WriteAttackDamageSlot
        // adds the passed-in damage (zero, for acquisition) onto an EXISTING identity+session slot rather than
        // re-seeding it -- so a monster's chase/give-up/re-acquire churn must never erase real damage a target
        // already accrued before being (re-)acquired. Character A lands real damage FIRST (50), is then
        // acquired by the monster's own AI (the zero-seed write-through against that SAME already-tracked
        // slot), and Character B separately lands less real damage (20) without ever being acquired. If
        // RegisterAcquisition wrongly re-zeroed A's slot, A's tracked value would read back as 0 and B (20)
        // would incorrectly win kill credit instead of A (50).
        var monsterRow = WorldDataTestRows.Monster(601) with
        {
            Life = 100_000,
            ItemLevel = 1,
            RealLevel = 1,
            SummonTime1 = 9999,
            SummonTime2 = 9999,
            FrameInfo1 = 1,
            FrameInfo3 = 1,
            // Large detection radius / small melee radius, same reasoning as MonsterAiSystemKillCreditTests:
            // keeps the monster in Chase (not fallen through to AttackWindup) long enough to observe the table.
            RadiusInfo1 = 2,
            RadiusInfo2 = 1000,
            WalkSpeed = 10,
            RunSpeed = 100,
            AttackType = 1 // SelectAvatarIndexForPossibleAttack's proactive-aggro gate: AttackType in {1,3,6}
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, 601) with
        {
            Number = 1, LocationX = 0, LocationY = 0, LocationZ = 0, Radius = 0
        };
        var rows = WorldDataTestRows.MinimalRows() with { Monsters = [monsterRow], MonsterSpawnRegions = [region] };
        var cache = WorldDataCacheBuilder.Build(rows).Cache;

        var scheduler = new MonsterSpawnScheduler(cache, static () => new ZeroScatterRandom());
        // Detection's own 50% coin flip is forced to always succeed so acquisition is deterministic.
        var ai = new MonsterAiSystem(new ScriptedRandomSource(0));
        var options = new GameServerOptions { AoiCellSize = 100_000f };
        var zone = ZoneTestKit.CreateZone(1, options, simulationSystems: [scheduler, ai], worldData: cache);

        EnterCharacter(zone, 10, "A");
        // First tick: MonsterSpawnScheduler pops the monster (Spawning -> Decision, FrameInfo1 == 1), but
        // TryAcquireTarget only runs for a monster already IN Decision at the START of a tick -- so this same
        // tick's Update dispatch still sees Spawning and never attempts acquisition, giving a clean window to
        // seed real damage before A is ever acquired.
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetMonster(1, out var monster));
        Assert.Equal(MonsterAiState.Decision, monster!.AiState);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 50, 10, out var diedFromRealHit, out _));
        Assert.False(diedFromRealHit);

        // TryAcquireTarget's own 1-second detection throttle (MonsterDetectionThrottleLegacyTicks == 2) means
        // the scan that actually picks A up doesn't run on the very next tick -- it takes a couple of ticks in
        // Decision for DetectionThrottleTicks to cross that threshold first. Retry (bounded) rather than
        // assert on a fixed tick count, same posture as MonsterAiSystemKillCreditTests' own AcquireTarget
        // helper -- calls RegisterAcquisition(10, ...) against the SAME already-tracked identity+session slot
        // the instant it succeeds.
        for (var i = 0; i < 10 && monster!.AiState != MonsterAiState.Chase; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out monster));
        }

        Assert.Equal(MonsterAiState.Chase, monster!.AiState);
        Assert.Equal(10, monster.TargetCharacterId);

        // B enters only after acquisition already completed, so it is never itself an AI candidate (the
        // monster is already locked in Chase and won't re-run TryAcquireTarget) -- B exists purely to give the
        // kill-credit scan a second, real-damage-only entry to compare A's (possibly-reset) slot against.
        EnterCharacter(zone, 11, "B");
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 20, 11, out var diedFromB, out _));
        Assert.False(diedFromB);

        Assert.True(zone.TryGetMonster(1, out monster));
        Assert.True(zone.TryDamageMonster(monster!.ServerIndex, monster.Life, null, out var died, out _));
        Assert.True(died);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Equal(10, deadMonster!.KillerCharacterId); // A's real 50 (preserved) > B's 20
    }

    [Fact]
    public void FiftyFirstAttacker_EvictsOldestSlot_NotLowestDamageSlot()
    {
        // FIFO-cap-50 edge case (WriteAttackDamageSlot: "if (_attackDamage.Count >= MaxAttackDamageEntries)
        // _attackDamage.RemoveAt(0)"): once the table is full, the OLDEST tracked identity+session slot is
        // discarded to make room for a new one -- regardless of how much damage that oldest slot recorded.
        // Attacker #1 lands the single LARGEST hit of the whole test (5000) -- if eviction were instead
        // "lowest damage first," #1's slot would be the very last one ever evicted. Attackers #2-#50 (49 of
        // them) each land a trivially small hit (1), filling the table to its 50-slot cap alongside #1.
        // Attacker #51 then lands a modest hit (10) -- more than any single one of #2-#50, but far less than
        // #1 -- forcing an eviction. Only a FIFO rule evicts #1 here (the oldest, despite recording the
        // highest damage by far); a lowest-damage rule would instead evict one of #2-#50 and leave #1 (5000)
        // as the still-tracked, still-winning entry. Kill credit going to #51 (10 > 1, and NOT #1) is the only
        // externally-observable proof the eviction is FIFO rather than damage-ranked.
        const int firstAttackerId = 1000;
        // The 51st distinct attacker to ever hit this monster -- one of the 51 characters entered below, but
        // deliberately the one held back from the #2-#50 fill loop so it lands the write that forces eviction.
        const int fiftyFirstAttackerId = firstAttackerId + 50;

        var zone = CreateZoneWithManualMonster(1, 1_000_000, out var monster);

        for (var i = 0; i < 51; i++)
            EnterCharacter(zone, firstAttackerId + i, $"Attacker{i}");
        zone.Tick(SimulationClock.LegacyTick); // InboxDrainCapPerTick (4096) comfortably drains all 51 Enters

        // #1: the single largest hit in the test -- would win kill credit outright if never evicted.
        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 5000, firstAttackerId, out var died1, out _));
        Assert.False(died1);

        // #2-#50: 49 more distinct attackers, each landing a trivially small hit -- brings the table to its
        // full 50-slot cap (#1 plus these 49).
        for (var i = 1; i <= 49; i++)
        {
            Assert.True(zone.TryDamageMonster(monster.ServerIndex, 1, firstAttackerId + i, out var diedFromFill,
                out _));
            Assert.False(diedFromFill);
        }

        // #51: the table is now full (50/50) -- this write must evict the OLDEST slot (#1, damage 5000), not
        // whichever slot currently holds the lowest recorded damage (which would be one of #2-#50, damage 1).
        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 10, fiftyFirstAttackerId, out var died51,
            out var remaining));
        Assert.False(died51);

        // Finishing blow bypasses damage-tracking (null attacker) so the kill-credit scan reads back exactly
        // the 50 entries left after the FIFO eviction above, undisturbed by a 52nd write.
        Assert.True(zone.TryGetMonster(1, out monster));
        Assert.True(zone.TryDamageMonster(monster!.ServerIndex, remaining, null, out var died, out _));
        Assert.True(died);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        // #51 (damage 10) wins -- #1 (damage 5000) was evicted despite being the highest-damage entry ever
        // recorded, proving eviction order is insertion-order (FIFO), not damage-ranked.
        Assert.Equal(fiftyFirstAttackerId, deadMonster!.KillerCharacterId);
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
