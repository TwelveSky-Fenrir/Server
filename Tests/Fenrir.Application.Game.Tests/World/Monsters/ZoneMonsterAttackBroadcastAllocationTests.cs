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

[Collection(AllocationRegressionCollection.Name)]
public class ZoneMonsterAttackBroadcastAllocationTests
{
    private static readonly EffectiveStats StrongAttacker =
        new(1000, 1000, 1000, 0, 100, 0, 0, 0, 0, 0, 0);

    private static readonly EffectiveStats ImmortalTarget =
        new(100_000_000, 1000, 0, 1_000_000, 100, 0, 0, 100_000, 0, 0, 100_000);

    private static MonsterRowDto PvmMonsterTemplate()
    {
        return WorldDataTestRows.Monster(700) with
        {
            Life = 10_000_000,
            AttackBlock = 0,
            DefensePower = 1_000_000
        };
    }

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
        spawned!.AiState = MonsterAiState.Decision;

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));
        foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);

        zone.Tick(SimulationClock.MonsterRebroadcastInterval + TimeSpan.FromSeconds(1));
        foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);

        return (zone, spawned, pipes);
    }

    private static long MeasurePvmAllocatedBytes(Zone zone, MonsterEntity monster,
        IReadOnlyList<FakeDuplexPipe> pipes, int iterations)
    {
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

        Assert.True(largePerCall < 1024,
            $"Expected < 1024 bytes/call for 40 AOI neighbors, was {largePerCall:F1} (2-neighbor baseline: {smallPerCall:F1}).");

        Assert.True(largePerCall <= smallPerCall + 512,
            $"Per-attack allocation scaled with AOI neighbor count: {smallPerCall:F1} bytes/call at 2 neighbors vs. {largePerCall:F1} bytes/call at 40 neighbors.");
    }

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
