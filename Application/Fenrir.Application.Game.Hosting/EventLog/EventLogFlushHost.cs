using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.EventLog;

/// <summary>
///     Composition root for the game.EventLog write-behind path: owns the one <see cref="EventLogQueue" />
///     instance, exposes it to <c>*.Services</c> producers as <see cref="IEventLogQueue" />, and drives its
///     drain loop as a <see cref="BackgroundService" /> -- same "one instance, three registrations" shape as
///     <c>PositionWriteBehindHost</c> (<see cref="IWriteBehindFlusher" />'s own composition root).
/// </summary>
/// <remarks>
///     The <see cref="EventLogQueue" /> constructor needs a flush callback closing over
///     <see cref="IEventLogRepository.BatchLogAsync" />, which is why it is built here rather than registered
///     directly by <c>Fenrir.Data</c>'s own <c>AddFenrirData</c> -- see that method's remarks.
/// </remarks>
public sealed class EventLogFlushHost : BackgroundService, IEventLogQueue
{
    private readonly EventLogQueue _queue;

    public EventLogFlushHost(IEventLogRepository eventLog, ILogger<EventLogFlushHost> logger)
    {
        _queue = new EventLogQueue(
            eventLog.BatchLogAsync,
            onFlushError: ex => logger.LogError(ex, "game.EventLog write-behind flush failed; batch dropped"),
            onDropped: count => logger.LogWarning(
                "game.EventLog write-behind queue full; dropped {Count} entr{Suffix} (sustained DB unavailability?)",
                count, count == 1 ? "y" : "ies"));
    }

    /// <inheritdoc />
    public bool Enqueue(EventLogEntryTvp entry)
    {
        return _queue.Enqueue(entry);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _queue.RunAsync(stoppingToken);
    }

    /// <summary>Idempotent -- safe whether or not <see cref="StopAsync" /> already disposed the queue.</summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await _queue.DisposeAsync().ConfigureAwait(false);
    }
}
