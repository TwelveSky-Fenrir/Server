using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.Center.WorldState;

public sealed class CenterAuthorityPreloadService(
    IWorldStateAuthority worldState,
    IHeroRankAuthority heroRank,
    TowerStoreAuthority towerStore,
    ILogger<CenterAuthorityPreloadService> logger)
{
    public async Task PreloadAllAsync(CancellationToken ct)
    {
        logger.LogInformation("Center authority preload starting (world, hero-rank, tower)");

        await worldState.InitializeAsync(ct).ConfigureAwait(false);
        await heroRank.InitializeAsync(ct).ConfigureAwait(false);
        await towerStore.InitializeAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Center authority preload complete");
    }
}
