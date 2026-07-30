using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.WriteBehind;

namespace Fenrir.CenterServer.Hosting;

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
