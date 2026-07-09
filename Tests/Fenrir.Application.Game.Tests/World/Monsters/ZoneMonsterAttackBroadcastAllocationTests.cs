using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     GC-allocation regression guard for the two <c>BroadcastAttackResult</c>-driven combat paths NOT already
///     covered by <c>ZoneCombatBroadcastAllocationTests</c> (which only exercises the PvP/mCase-2
///     <c>CombatRecipients</c> path): <c>ApplyPvmAttack</c>'s <c>_pvmAttackRecipientScratch</c> / shared
///     <c>_combatNeighborScratch</c> reuse (Avatar -&gt; Monster, mCase 3, <c>Zone.Combat.cs</c>) and
///     <c>ResolveMonsterAttack</c>'s <c>_mvpAttackNeighborScratch</c> / <c>_mvpAttackRecipientScratch</c> reuse
///     (Monster -&gt; Avatar, AI-initiated MvP attack, <c>Zone.Monsters.cs</c>). Before those fixes, both built a
///     fresh per-attack <c>HashSet&lt;int&gt;</c> from a raw <c>AoiGrid.Neighbors(...)</c> enumerable. This proves
///     per-attack allocated bytes stays flat as the AOI bystander count grows by a wide margin (2 vs. 40), for
///     each path independently.
/// </summary>
/// <remarks>
///     Every recipient pipe is drained between measured iterations (outside the counted before/after window) --
///     same <see cref="FakeDuplexPipe" />-never-reads-what-it-writes artifact as
///     <c>ZoneCombatBroadcastAllocationTests</c>'s own remarks describe, unrelated to the code under test.
///     Hit/critical/variance rolls are all pinned to their most favorable-for-the-attacker outcome via a fixed
///     <c>ScriptedRandomSource(0, 0)</c> (every roll returns 0, which always beats a positive percent threshold),
///     deliberately combined with a wildly lopsided defense stat on the non-attacking side so the resulting
///     per-hit damage still floors to a trivial 1-2 points -- keeping the target alive (and away from the kill/
///     death path's own, unrelated allocation profile) across the full 200-iteration measurement loop.
/// </remarks>
[Collection(AllocationRegressionCollection.Name)]
public class ZoneMonsterAttackBroadcastAllocationTests
{
    private static readonly EffectiveStats StrongAttacker =
        new(1000, 1000, 1000, 0, 100, 0, 0, 0, 0, 0, 0);

    private static readonly EffectiveStats ImmortalTarget =
        new(100_000_000, 1000, 0, 1_000_000, 100, 0, 0, 100_000, 0, 0, 100_000);

    // The target's DefensePower deliberately absurd so ApplyPvmAttack's damage floors to 1 every hit -- the
    // monster survives hundreds of repeated hits across the measurement loop without ever dying (a death would
    // additionally run monster-kill loot/despawn machinery, an unrelated allocation source this test isn't
    // targeting).
    private static MonsterRowDto PvmMonsterTemplate()
    {
        return WorldDataTestRows.Monster(700) with
        {
            Life = 10_000_000,
            AttackBlock = 0,
            DefensePower = 1_000_000
        };
    }

    // AttackPower deliberately modest and the target's own DefensePower deliberately absurd (see
    // ImmortalTarget) so ResolveMonsterAttack's damage floors to 1-2 every hit -- same non-lethal-across-the-
    // loop rationale as PvmMonsterTemplate, mirrored onto the MvP direction.
    private static MonsterRowDto MvpMonsterTemplate()
    {
        return WorldDataTestRows.Monster(700) with
        {
            AttackPower = 10,
            AttackSuccess = 100,
            AttackBlock = 0,
            Critical = 0
        };
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

    /// <summary>
    ///     One attacker plus a stationary, effectively-unkillable monster (same position as the attacker, so
    ///     the shared <c>MaxAttackDistance</c>/range check is trivially satisfied) plus <paramref name="bystanderCount" />
    ///     extra players clustered in the same AOI cell -- every one of them is a genuine
    ///     <c>_pvmAttackRecipientScratch</c> fan-out target for the attack posted below.
    /// </summary>
    private static (Zone Zone, MonsterEntity Monster, List<FakeDuplexPipe> Pipes) BuildPvmZone(int bystanderCount)
    {
        var zone = ZoneTestKit.CreateZone(1, randomSource: new ScriptedRandomSource(0, 0));
        var pipes = new List<FakeDuplexPipe>();

        var (attackerSession, attackerPipe) = ZoneTestKit.CreateSession(1);
        pipes.Add(attackerPipe);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(attackerSession, 1, "Attacker")));

        var monster = MonsterEntity.Create(1, 1u, PvmMonsterTemplate(), 1, 100f, 0f, 100f, 50f);
        zone.SpawnMonster(monster);

        for (var i = 0; i < bystanderCount; i++)
        {
            var characterId = 100 + i;
            var (session, pipe) = ZoneTestKit.CreateSession(characterId);
            pipes.Add(pipe);
            zone.Post(ZoneCommand.Enter(characterId,
                ZoneTestKit.EnterData(session, 1, $"Bystander{i}", 100f + i * 3f)));
        }

        zone.Tick(TimeSpan.FromMilliseconds(50));
        foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        attacker!.Stats = StrongAttacker;
        attacker.AttackSubPacketCeiling = int.MaxValue;

        Assert.True(zone.TryGetMonster(1, out var spawned));
        // CheckPossibleAttackTarget rejects a monster mid-spawn-windup -- force straight to Decision, same as
        // ZoneMonsterCombatTests.CreateZoneWithSpawnedMonster.
        spawned!.AiState = MonsterAiState.Decision;

        // Past the attacker's own zone-entry protect window -- ResolvePvmAttack only gates the attacker side.
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));
        foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);

        // Force Zone.Monsters.cs's own, unrelated 5 s RebroadcastMonsters keep-alive to fire (and its own
        // separate _monsterBroadcastNeighborScratch buffer to grow to full size) HERE, outside the measured
        // window below -- otherwise it would fire for the first time mid-measurement once cumulative elapsed
        // ticks cross its threshold, and that one-time buffer growth scales with bystanderCount just like the
        // scratch buffers this test actually targets, contaminating the signal with an unrelated allocation
        // source (already using its own correct rent-once/scratch-buffer idiom -- not a regression, just noise
        // this harness must not let leak into the measurement).
        zone.Tick(SimulationClock.MonsterRebroadcastInterval + TimeSpan.FromSeconds(1));
        foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);

        return (zone, spawned, pipes);
    }

    private static long MeasurePvmAllocatedBytes(Zone zone, MonsterEntity monster,
        IReadOnlyList<FakeDuplexPipe> pipes, int iterations)
    {
        // Small, fixed per-iteration delta -- keeps this loop's own cumulative elapsed time (iterations * 10 ms)
        // far under RebroadcastMonsters' 5 s interval, so that unrelated keep-alive never fires again inside
        // the measured window after BuildPvmZone's own deliberate warm-up tick above already reset it.
        var tickDelta = TimeSpan.FromMilliseconds(10);

        zone.PostCombatCommand(new CombatCommand
            { AttackerCharacterId = 1, AttackInfo = MeleeAgainstMonster(1, monster) });
        zone.Tick(tickDelta);
        foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);

        long total = 0;
        for (var i = 0; i < iterations; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            zone.PostCombatCommand(new CombatCommand
                { AttackerCharacterId = 1, AttackInfo = MeleeAgainstMonster(1, monster) });
            zone.Tick(tickDelta);
            total += GC.GetAllocatedBytesForCurrentThread() - before;

            foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);
        }

        return total;
    }

    [Fact]
    public void RepeatedPvmAttacks_PerCallAllocation_DoesNotScaleWithAoiNeighborCount()
    {
        const int iterations = 200;

        var (smallZone, smallMonster, smallPipes) = BuildPvmZone(2);
        var smallPerCall =
            MeasurePvmAllocatedBytes(smallZone, smallMonster, smallPipes, iterations) / (double)iterations;

        var (largeZone, largeMonster, largePipes) = BuildPvmZone(40);
        var largePerCall =
            MeasurePvmAllocatedBytes(largeZone, largeMonster, largePipes, iterations) / (double)iterations;

        // Generous absolute ceiling regardless of neighbor count -- see ZoneCombatBroadcastAllocationTests'
        // own remarks for the same reasoning applied to the PvP path.
        Assert.True(largePerCall < 1024,
            $"Expected < 1024 bytes/call for 40 AOI neighbors, was {largePerCall:F1} (2-neighbor baseline: {smallPerCall:F1}).");

        // The direct scaling check: 20x more AOI neighbors must not meaningfully increase per-call allocation.
        Assert.True(largePerCall <= smallPerCall + 512,
            $"Per-attack allocation scaled with AOI neighbor count: {smallPerCall:F1} bytes/call at 2 neighbors vs. {largePerCall:F1} bytes/call at 40 neighbors.");
    }

    /// <summary>
    ///     One effectively-unkillable target plus <paramref name="bystanderCount" /> extra players clustered in
    ///     the same AOI cell, all within <c>ResolveMonsterAttack</c>'s (player-side) AOI-grid neighbor scan of
    ///     the target's own position -- the monster itself is a free-standing <see cref="MonsterEntity" /> never
    ///     registered with the zone (<c>ResolveMonsterAttack</c> never looks it up by server index; the caller,
    ///     normally <c>MonsterAiSystem</c>, already holds the reference), so it needs no grid/AOI presence of its
    ///     own for this test.
    /// </summary>
    private static (Zone Zone, MonsterEntity Monster, List<FakeDuplexPipe> Pipes) BuildMvpZone(int bystanderCount)
    {
        var zone = ZoneTestKit.CreateZone(1, randomSource: new ScriptedRandomSource(0, 0));
        var pipes = new List<FakeDuplexPipe>();

        var (targetSession, targetPipe) = ZoneTestKit.CreateSession(1);
        pipes.Add(targetPipe);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(targetSession, 1, "Target")));

        for (var i = 0; i < bystanderCount; i++)
        {
            var characterId = 100 + i;
            var (session, pipe) = ZoneTestKit.CreateSession(characterId);
            pipes.Add(pipe);
            zone.Post(ZoneCommand.Enter(characterId,
                ZoneTestKit.EnterData(session, 1, $"Bystander{i}", 100f + i * 3f)));
        }

        zone.Tick(TimeSpan.FromMilliseconds(50));
        foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(1, out var target));
        target!.Stats = ImmortalTarget;
        target.MaxLife = ImmortalTarget.MaxLife;
        target.Life = ImmortalTarget.MaxLife;

        // Past the target's own zone-entry protect window -- ResolveMvpAttack only gates the defender side.
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));
        foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);

        var monster = MonsterEntity.Create(1, 1u, MvpMonsterTemplate(), 1, 100f, 0f, 100f, 50f);
        return (zone, monster, pipes);
    }

    private static long MeasureMvpAllocatedBytes(Zone zone, MonsterEntity monster,
        IReadOnlyList<FakeDuplexPipe> pipes, int iterations)
    {
        zone.ResolveMonsterAttack(monster, 1);
        foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);

        long total = 0;
        for (var i = 0; i < iterations; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            zone.ResolveMonsterAttack(monster, 1);
            total += GC.GetAllocatedBytesForCurrentThread() - before;

            foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);
        }

        return total;
    }

    [Fact]
    public void RepeatedMvpAttacks_PerCallAllocation_DoesNotScaleWithAoiNeighborCount()
    {
        const int iterations = 200;

        var (smallZone, smallMonster, smallPipes) = BuildMvpZone(2);
        var smallPerCall =
            MeasureMvpAllocatedBytes(smallZone, smallMonster, smallPipes, iterations) / (double)iterations;

        var (largeZone, largeMonster, largePipes) = BuildMvpZone(40);
        var largePerCall =
            MeasureMvpAllocatedBytes(largeZone, largeMonster, largePipes, iterations) / (double)iterations;

        Assert.True(largePerCall < 1024,
            $"Expected < 1024 bytes/call for 40 AOI neighbors, was {largePerCall:F1} (2-neighbor baseline: {smallPerCall:F1}).");

        Assert.True(largePerCall <= smallPerCall + 512,
            $"Per-attack allocation scaled with AOI neighbor count: {smallPerCall:F1} bytes/call at 2 neighbors vs. {largePerCall:F1} bytes/call at 40 neighbors.");
    }
}
