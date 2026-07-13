using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

public sealed class MonsterLootFlushHost(
    ZoneRegistry zones,
    ICharacterRepository characters,
    ILogger<MonsterLootFlushHost> logger) : BackgroundService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(FlushInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await FlushAllZonesAsync(stoppingToken).ConfigureAwait(false);

            var wake = zones.Zones.Select(zone => zone.WaitForMoneyGrantAsync(stoppingToken)).ToArray();
            var timerTick = timer.WaitForNextTickAsync(stoppingToken).AsTask();

            try
            {
                await Task.WhenAny(timerTick, Task.WhenAny(wake)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task FlushAllZonesAsync(CancellationToken stoppingToken)
    {
        foreach (var zone in zones.Zones)
        {
            var grants = zone.DrainPendingMoneyGrants();
            if (grants.Count == 0)
                continue;

            foreach (var (characterId, amount) in grants)
                try
                {
                    await characters.AdjustMoneyAsync(characterId, amount, 0, stoppingToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex,
                        "Zone {MapId}: failed to persist a {Amount}-money kill grant for character {CharacterId}",
                        zone.MapId, amount, characterId);
                }
        }
    }
}
