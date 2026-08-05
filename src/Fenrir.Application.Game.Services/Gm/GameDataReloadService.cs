using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Data.Abstractions.World;
using Fenrir.Domain.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

public sealed class GameDataReloadService(
    WorldDataLoader worldDataLoader,
    WorldDataCache worldData,
    QuestCatalog questCatalog,
    MonsterSpawnScheduler monsterSpawnScheduler,
    CommerceCatalogCache commerceCatalog,
    IWorldDataRepository repository,
    ILogger<GameDataReloadService> logger) : IGameDataReloadService
{
    private readonly SemaphoreSlim _reloadGate = new(1, 1);

    public async ValueTask<GameDataReloadOutcome> ReloadAsync(GameDataReloadScope scope,
        CancellationToken cancellationToken)
    {
        try
        {
            await _reloadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new GameDataReloadOutcome(false, scope, 0, 0, 0, TimeSpan.Zero, "Cancelled");
        }

        try
        {
            try
            {
                var result = await worldDataLoader.ReloadAsync(cancellationToken).ConfigureAwait(false);
                questCatalog.Reload(worldData);

                if (scope is GameDataReloadScope.All or GameDataReloadScope.Monsters)
                    monsterSpawnScheduler.RequestConfigurationReload();

                if (scope is GameDataReloadScope.All or GameDataReloadScope.Items)
                    await commerceCatalog.RefreshAllAsync(repository, cancellationToken).ConfigureAwait(false);

                logger.LogInformation(
                    "GM game-data reload completed: scope {Scope}, {Items} items, {Monsters} monsters, {Quests} quests in {ElapsedMs:F0} ms",
                    scope, result.ItemCount, result.MonsterCount, result.QuestCount, result.Elapsed.TotalMilliseconds);

                return new GameDataReloadOutcome(true, scope, result.ItemCount, result.MonsterCount, result.QuestCount,
                    result.Elapsed, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new GameDataReloadOutcome(false, scope, 0, 0, 0, TimeSpan.Zero, "Cancelled");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "GM game-data reload failed for scope {Scope}; the last complete snapshot remains active", scope);
                return new GameDataReloadOutcome(false, scope, 0, 0, 0, TimeSpan.Zero, "ReloadFailed");
            }
        }
        finally
        {
            _reloadGate.Release();
        }
    }
}
