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

        // PeriodicTimer.WaitForNextTickAsync permits only one outstanding call at a time; a fresh call
        // issued while a prior one is still pending throws InvalidOperationException. Every racing task
        // below (the timer tick and each zone's wake signal) is therefore created ONCE and held across
        // loop iterations, and only the task that actually won the race is ever replaced -- mirrors
        // Zone.RunAsync (src/Fenrir.Application.Game/Domain/World/Zone.cs) and
        // PositionWriteBehindHost.RetryPendingDisconnectFlushesAsync, which use the same idiom.
        var zoneList = zones.Zones.ToArray();
        var wake = new Task[zoneList.Length];
        for (var i = 0; i < zoneList.Length; i++)
            wake[i] = zoneList[i].WaitForMoneyGrantAsync(stoppingToken);

        var timerTick = timer.WaitForNextTickAsync(stoppingToken).AsTask();

        while (!stoppingToken.IsCancellationRequested)
        {
            await FlushAllZonesAsync(stoppingToken).ConfigureAwait(false);

            Task woken;
            try
            {
                var candidates = new Task[wake.Length + 1];
                Array.Copy(wake, candidates, wake.Length);
                candidates[wake.Length] = timerTick;
                woken = await Task.WhenAny(candidates).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

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
