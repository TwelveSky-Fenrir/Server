using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     GC-allocation regression guard for the rent-once/write-once <c>BroadcastAttackResult</c> idiom and the
///     reused <c>_combatNeighborScratch</c>/<c>_combatRecipientScratch</c> AOI-neighbor scratch buffers in
///     <c>Zone.Combat.cs</c>. Before that fix, <c>CombatRecipients</c> built a fresh per-attack recipient set via
///     a LINQ <c>.Where()</c>/<c>.ToArray()</c> pipeline plus a freshly allocated <c>HashSet&lt;int&gt;</c>, and
///     <c>BroadcastAttackResult</c> independently re-serialized the identical <c>AttackResponse</c> once per
///     recipient via <c>Session.Send</c> -- both of which scale with the number of AOI neighbors in combat range.
///     This test proves per-attack allocated bytes stays flat as that neighbor count grows by a wide margin (2
///     vs. 40 bystanders): a regression to either per-call allocation pattern would show up as a large jump
///     between the two, which the assertions below catch.
/// </summary>
/// <remarks>
///     Every recipient pipe is drained between measured iterations (outside the counted before/after window) --
///     without that, <see cref="FakeDuplexPipe" />'s underlying <see cref="System.IO.Pipelines.Pipe" /> would
///     keep accumulating never-read bytes across all 200 iterations and eventually need brand-new backing
///     segments once its current one fills, an artifact of this in-memory test harness never actually reading
///     what it writes (a real session's socket-flush loop continuously drains), not of the broadcast code under
///     test -- and that artifact itself scales with recipient count (more pipes being written to per call), which
///     would otherwise completely swamp the signal this test is actually after.
/// </remarks>
[Collection(AllocationRegressionCollection.Name)]
public class ZoneCombatBroadcastAllocationTests
{
    private static readonly EffectiveStats StrongAttacker =
        new(1000, 1000, 1000, 0, 100, 0, 0, 0, 0, 0, 0);

    // MaxLife/DefensePower deliberately absurd -- this defender must survive hundreds of repeated hits across
    // the measurement loop below without ever dying (a death would additionally run ApplyPvpKillRewards, an
    // unrelated allocation source this test isn't targeting).
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

    /// <summary>
    ///     One attacker (tribe 0), one effectively-unkillable defender (tribe 1), plus
    ///     <paramref name="bystanderCount" /> extra tribe-1 players clustered within the same AOI cell as both --
    ///     every one of them is a genuine <c>CombatRecipients</c>/<c>BroadcastAttackResult</c> fan-out target for
    ///     the attack posted below, not incidental noise. Returns every recipient's pipe alongside the zone so the
    ///     caller can drain them between measured iterations (see class remarks).
    /// </summary>
    private static (Zone Zone, List<FakeDuplexPipe> Pipes) BuildZone(int bystanderCount)
    {
        var zone = ZoneTestKit.CreateZone(1, randomSource: new ScriptedRandomSource(0, 0));
        var pipes = new List<FakeDuplexPipe>();

        var (attackerSession, attackerPipe) = ZoneTestKit.CreateSession(1);
        pipes.Add(attackerPipe);
        zone.Post(ZoneCommand.Enter(1,
            ZoneTestKit.EnterData(attackerSession, 1, "Attacker", 100f, 0f, 100f, tribe: 0)));

        var (defenderSession, defenderPipe) = ZoneTestKit.CreateSession(2);
        pipes.Add(defenderPipe);
        zone.Post(ZoneCommand.Enter(2,
            ZoneTestKit.EnterData(defenderSession, 1, "Defender", 105f, 0f, 100f, tribe: 1)));

        for (var i = 0; i < bystanderCount; i++)
        {
            var characterId = 100 + i;
            var (session, pipe) = ZoneTestKit.CreateSession(characterId);
            pipes.Add(pipe);
            zone.Post(ZoneCommand.Enter(characterId,
                ZoneTestKit.EnterData(session, 1, $"Bystander{i}", 100f + i * 3f, 0f, 100f, tribe: 1)));
        }

        zone.Tick(TimeSpan.FromMilliseconds(50));
        foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        attacker!.Stats = StrongAttacker;
        defender!.Stats = ImmortalDefender;
        defender.MaxLife = ImmortalDefender.MaxLife;
        defender.Life = ImmortalDefender.MaxLife;
        // Legal, already-acting pose -- see ZoneAttackTests.TwoPlayerZone's own remarks for why this is required.
        defender.ActionSort = 1;
        attacker.AttackSubPacketCeiling = int.MaxValue;

        // Past both sides' zone-entry protect window, else every attack below is rejected (never reaching
        // BroadcastAttackResult at all) as Attacker/DefenderProtected.
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));
        foreach (var pipe in pipes) ZoneTestKit.DrainOutbound(pipe);

        return (zone, pipes);
    }

    /// <summary>
    ///     Warms up JIT/ArrayPool once (untracked), then measures bytes allocated across <paramref name="iterations" />
    ///     repeated attacks -- each iteration's own before/after snapshot excludes the subsequent pipe drain (see
    ///     class remarks), so only the attack call itself is charged.
    /// </summary>
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

        // Writing the same already-serialized frame to N independent per-recipient pipes has an inherent, small
        // linear cost of its own (observed ~35-50 bytes/recipient from System.IO.Pipelines bookkeeping around
        // Transport.Output.GetSpan/FlushAsync) that no broadcast implementation can eliminate -- reaching N
        // distinct sockets fundamentally requires N distinct writes. That is NOT what this test guards against.
        // What it guards against is the MUCH larger jump a regression back to either the old per-call
        // HashSet<int>/LINQ .Where().ToArray() recipient-collection build (whose backing arrays alone would cost
        // on the order of 1-2 KB for ~40 elements after a handful of resizes) or a per-recipient full
        // AttackResponse re-serialization would introduce -- a step change several times bigger than the
        // ~100 bytes/recipient budgeted below.
        const int maxBytesPerRecipient = 100;
        var recipientCountAtLarge = largeBystanders + 2; // + attacker + defender
        var maxAbsoluteBytes = recipientCountAtLarge * maxBytesPerRecipient;
        var maxAllowedDelta = (largeBystanders - smallBystanders) * maxBytesPerRecipient;

        Assert.True(largePerCall < maxAbsoluteBytes,
            $"Expected < {maxAbsoluteBytes} bytes/call for {largeBystanders} AOI neighbors, was {largePerCall:F1} (2-neighbor baseline: {smallPerCall:F1}).");

        Assert.True(largePerCall <= smallPerCall + maxAllowedDelta,
            $"Per-attack allocation scaled with AOI neighbor count far beyond the expected ~{maxBytesPerRecipient} bytes/recipient pipe-write floor: {smallPerCall:F1} bytes/call at {smallBystanders} neighbors vs. {largePerCall:F1} bytes/call at {largeBystanders} neighbors.");
    }
}
