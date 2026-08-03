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

        var zoneList = zones.Zones.ToArray();
        var wake = new Task[zoneList.Length];
        for (var i = 0; i < zoneList.Length; i++)
            wake[i] = zoneList[i].WaitForMoneyGrantAsync(stoppingToken);

        var timerTick = timer.WaitForNextTickAsync(stoppingToken).AsTask();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await FlushAllZonesAsync(stoppingToken).ConfigureAwait(false);

                var candidates = new Task[wake.Length + 1];
                Array.Copy(wake, candidates, wake.Length);
                candidates[wake.Length] = timerTick;
                var woken = await Task.WhenAny(candidates).ConfigureAwait(false);

                if (ReferenceEquals(woken, timerTick))
                {
                    timerTick = timer.WaitForNextTickAsync(stoppingToken).AsTask();
                    continue;
                }

                for (var i = 0; i < wake.Length; i++)
                {
                    if (!ReferenceEquals(wake[i], woken))
                        continue;

                    wake[i] = zoneList[i].WaitForMoneyGrantAsync(stoppingToken);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        await FlushAllZonesAsync(CancellationToken.None).ConfigureAwait(false);
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
