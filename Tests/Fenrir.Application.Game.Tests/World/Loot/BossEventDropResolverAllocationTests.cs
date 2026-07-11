using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Loot;

[Collection(AllocationRegressionCollection.Name)]
public class BossEventDropResolverAllocationTests
{
    private const int NonBossMonsterId = 99999;

    [Fact]
    public void Resolve_NonBossMonster_AllocatesNothing()
    {
        var catalog = BossDropCatalog.Default;
        var worldData = ZoneTestKit.EmptyWorldData();
        var random = new Random(1);

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
