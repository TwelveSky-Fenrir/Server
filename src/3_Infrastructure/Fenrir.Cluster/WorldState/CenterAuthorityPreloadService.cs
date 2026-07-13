using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.WorldState;

/// <summary>
///     Orchestrates the one-time boot preload of every Center authoritative aggregate (world, hero-rank, tower)
///     before the accept loop and the cadence hosts start -- the Center equivalent of the GameServer's
///     synchronous "load every cache before RunAsync" rule.
/// </summary>
/// <remarks>
///     Reimplemented from <c>MyGame::Init</c> (Server/ts25center/S07_MyGame01.cpp:7-193): boot is fatal if the
///     world or tribe aggregate fails to load, so the process refuses to serve empty authoritative state. Any
///     exception here propagates to <c>Program.cs</c>, which fails boot. Called under a bounded (60s) timeout by
///     the caller.
/// </remarks>
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
