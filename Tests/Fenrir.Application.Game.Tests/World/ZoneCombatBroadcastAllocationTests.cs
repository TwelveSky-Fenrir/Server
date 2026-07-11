using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

[Collection(AllocationRegressionCollection.Name)]
public class ZoneCombatBroadcastAllocationTests
{
    private static readonly EffectiveStats StrongAttacker =
        new(1000, 1000, 1000, 0, 100, 0, 0, 0, 0, 0, 0);

    private static readonly EffectiveStats ImmortalDefender =
        new(100_000_000, 1000, 0, 100_000, 100, 100_000, 0, 100_000, 0, 0, 100_000);

    private static AttackForProtocol MeleeRequest()
    {
        return new AttackForProtocol
        {
            Case = 2,
            ServerIndex1 = 1,
            UniqueNumber1 = 1,
            ServerIndex2 = 2,
            UniqueNumber2 = 2,
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

    private static (Zone Zone, List<FakeDuplexPipe> Pipes) BuildZone(int bystanderCount)
    {
        var zone = ZoneTestKit.CreateZone(1, randomSource: new ScriptedRandomSource(0, 0));
        var pipes = new List<FakeDuplexPipe>();

        var (attackerSession, attackerPipe) = ZoneTestKit.CreateSession(1);
        pipes.Add(attackerPipe);
        zone.Post(ZoneCommand.Enter(1,
            ZoneTestKit.EnterData(attackerSession, 1, "Attacker", tribe: 0)));

        var (defenderSession, defenderPipe) = ZoneTestKit.CreateSession(2);
        pipes.Add(defenderPipe);
        zone.Post(ZoneCommand.Enter(2,
            ZoneTestKit.EnterData(defenderSession, 1, "Defender", 105f)));

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
        Assert.True(zone.TryGetPlayer(2, out var defender));
        attacker!.Stats = StrongAttacker;
        defender!.Stats = ImmortalDefender;
        defender.MaxLife = ImmortalDefender.MaxLife;
        defender.Life = ImmortalDefender.MaxLife;
        defender.ActionSort = 1;
        attacker.AttackSubPacketCeiling = int.MaxValue;

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));
        foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);

        return (zone, pipes);
    }

    private static long MeasureAllocatedBytes(Zone zone, IReadOnlyList<FakeDuplexPipe> pipes, int iterations)
    {
        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest() });
        zone.Tick(TimeSpan.FromMilliseconds(50));
        foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);

        long total = 0;
        for (var i = 0; i < iterations; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest() });
            zone.Tick(TimeSpan.FromMilliseconds(50));
            total += GC.GetAllocatedBytesForCurrentThread() - before;

            foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);
        }

        return total;
    }

    [Fact]
    public void RepeatedAttacks_PerCallAllocation_DoesNotScaleWithAoiNeighborCount()
    {
        const int iterations = 200;
        const int smallBystanders = 2;
        const int largeBystanders = 40;

        var (smallZone, smallPipes) = BuildZone(smallBystanders);
        var smallPerCall = MeasureAllocatedBytes(smallZone, smallPipes, iterations) / (double)iterations;

        var (largeZone, largePipes) = BuildZone(largeBystanders);
        var largePerCall = MeasureAllocatedBytes(largeZone, largePipes, iterations) / (double)iterations;

        const int maxBytesPerRecipient = 100;
        var recipientCountAtLarge = largeBystanders + 2;
        var maxAbsoluteBytes = recipientCountAtLarge * maxBytesPerRecipient;
        var maxAllowedDelta = (largeBystanders - smallBystanders) * maxBytesPerRecipient;

        Assert.True(largePerCall < maxAbsoluteBytes,
            $"Expected < {maxAbsoluteBytes} bytes/call for {largeBystanders} AOI neighbors, was {largePerCall:F1} (2-neighbor baseline: {smallPerCall:F1}).");

        Assert.True(largePerCall <= smallPerCall + maxAllowedDelta,
            $"Per-attack allocation scaled with AOI neighbor count far beyond the expected ~{maxBytesPerRecipient} bytes/recipient pipe-write floor: {smallPerCall:F1} bytes/call at {smallBystanders} neighbors vs. {largePerCall:F1} bytes/call at {largeBystanders} neighbors.");
    }
}
