using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.EventLog;

public sealed class EventLogFlushHost : BackgroundService, IEventLogQueue
{
    private readonly EventLogQueue _queue;

    public EventLogFlushHost(IEventLogRepository eventLog, ILogger<EventLogFlushHost> logger)
    {
        _queue = new EventLogQueue(
            eventLog.BatchLogAsync,
            onFlushError: (ex, count) => logger.LogError(ex,
                "game.EventLog write-behind flush failed; batch of {Count} entr{Suffix} dropped by design " +
                "(see EventLogQueue remarks -- no retry, no requeue)",
                count, count == 1 ? "y" : "ies"),
            onDropped: count => logger.LogWarning(
                "game.EventLog write-behind queue full; dropped {Count} entr{Suffix} (sustained DB unavailability?)",
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
