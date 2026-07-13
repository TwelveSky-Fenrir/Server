using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.CenterServer.Hosting;

/// <summary>
/// Pipeline write-behind de <c>game.EventLog</c> côté CenterServer : l'ingesteur op33 audite en catégorie
/// <c>AntiCheat</c> chaque <c>tSort</c> hors allowlist (durcissement — les events inconnus sont droppés au lieu
/// d'être rebroadcastés). Réplique le pattern de l'<c>EventLogFlushHost</c> du GameServer (que le Center ne peut
/// référencer, il vit dans <c>Application.Game</c>) : enveloppe le moteur <see cref="EventLogQueue"/> (Data) avec
/// <see cref="IEventLogRepository.BatchLogAsync"/> et l'expose comme <see cref="IEventLogQueue"/>.
/// </summary>
public sealed class CenterEventLogHost : BackgroundService, IEventLogQueue
{
    private readonly EventLogQueue _queue;

    public CenterEventLogHost(IEventLogRepository eventLog, ILogger<CenterEventLogHost> logger)
    {
        _queue = new EventLogQueue(
            eventLog.BatchLogAsync,
            onFlushError: (ex, count) => logger.LogError(ex,
                "game.EventLog write-behind flush failed on CenterServer; batch of {Count} entr{Suffix} dropped by " +
                "design (no retry, no requeue)",
                count, count == 1 ? "y" : "ies"),
            onDropped: count => logger.LogWarning(
                "game.EventLog write-behind queue full on CenterServer; dropped {Count} entr{Suffix}",
                count, count == 1 ? "y" : "ies"));
    }

    public bool Enqueue(EventLogEntryTvp entry)
    {
        return _queue.Enqueue(entry);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _queue.RunAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await _queue.DisposeAsync().ConfigureAwait(false);
    }
}
