using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Loot;

/// <summary>
///     GC-allocation regression guard for the <see cref="BossDropOutcome.None" /> path -- the ~99% of monster
///     kills whose id matches no boss/event block. <see cref="BossEventDropResolver.Resolve" /> runs on the tick
///     thread inside <c>MonsterSpawnScheduler.ProcessDeath</c>, so this hot no-op case must stay allocation-free:
///     the switch resolves to the shared static <see cref="BossDropOutcome.None" /> value struct, no pool is
///     built, no list is materialized.
/// </summary>
/// <remarks>
///     Joins the serialized <see cref="AllocationRegressionCollection" /> so it never runs concurrently with the
///     other allocation-measurement loops (see that class's own remarks). Per this repo's known ~10% flake note
///     for allocation/random tests under concurrent CPU load, a single failure warrants one re-run before being
///     treated as a real regression.
/// </remarks>
[Collection(AllocationRegressionCollection.Name)]
public class BossEventDropResolverAllocationTests
{
    private const int NonBossMonsterId = 99999;

    [Fact]
    public void Resolve_NonBossMonster_AllocatesNothing()
    {
        var catalog = BossDropCatalog.Default;
        WorldDataCache worldData = ZoneTestKit.EmptyWorldData();
        var random = new Random(1);

        // Warm up past tiered JIT (untracked) so the measured window reflects steady-state codegen only.
        for (var i = 0; i < 1_000; i++)
            _ = BossEventDropResolver.Resolve(NonBossMonsterId, 0, random, worldData, catalog);

        const int iterations = 100_000;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
            _ = BossEventDropResolver.Resolve(NonBossMonsterId, 0, random, worldData, catalog);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}
