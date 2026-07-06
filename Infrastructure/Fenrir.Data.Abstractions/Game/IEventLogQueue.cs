namespace Fenrir.Data.Abstractions.Game;

/// <summary>
///     Non-blocking producer handle for the game.EventLog write-behind path -- a hot gameplay call site (an
///     enchant/refine/socket/rune roll) only needs this narrow surface, not the concrete queue's own lifecycle
///     (<c>RunAsync</c>/<c>DisposeAsync</c>), same "non-generic handle" reasoning as <c>IWriteBehindFlusher</c>.
/// </summary>
/// <remarks>
///     Declared here, alongside <see cref="IEventLogRepository" />, rather than next to its implementation
///     (<c>Fenrir.Data.WriteBehind.EventLogQueue</c>) -- unlike <c>IWriteBehindFlusher</c>/<c>DirtyTracker</c>,
///     whose only producers live in <c>*.Domain</c>/<c>*.Hosting</c> (both carry the named WriteBehind
///     project-reference exception to <c>Fenrir.Data</c> directly), this queue's producers are ordinary
///     <c>*.Services</c> classes (e.g. enchant/refine/socket/rune-per-attempt logging), and <c>*.Services</c> is
///     never granted that exception -- it depends on <c>Fenrir.Data.Abstractions</c> only. Same
///     Abstractions/Implementation split as every repository interface in this folder: <c>*.Hosting</c>
///     constructs the real <c>EventLogQueue</c> (closing over <see cref="IEventLogRepository.BatchLogAsync" />)
///     and registers it under this interface for <c>*.Services</c> to consume.
/// </remarks>
public interface IEventLogQueue
{
    /// <summary>
    ///     Non-blocking, synchronous enqueue. Never awaits, never throws on backpressure. Returns false only
    ///     if the bounded channel is full (sustained DB unavailability); the entry is then silently dropped --
    ///     a hot packet-processing call site must never block on this call, though it may still choose to log
    ///     a false return for observability. This is best-effort telemetry for low-stakes events, never the
    ///     durability-critical path (see <see cref="IEventLogRepository.LogAsync" /> for that).
    /// </summary>
    public bool Enqueue(EventLogEntryTvp entry);
}
