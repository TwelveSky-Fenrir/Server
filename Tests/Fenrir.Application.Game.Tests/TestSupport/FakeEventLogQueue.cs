using Fenrir.Data.Abstractions.Game;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for IEventLogQueue: records every enqueued row (append-only) so item-modification
///     service test suites can assert a per-attempt audit row was (or was not) queued, without a real
///     write-behind channel/background host.
/// </summary>
internal sealed class FakeEventLogQueue : IEventLogQueue
{
    public List<EventLogEntryTvp> Enqueued { get; } = [];

    /// <summary>When true, <see cref="Enqueue" /> reports backpressure (bounded channel full) instead of accepting.</summary>
    public bool RejectNext { get; set; }

    public bool Enqueue(EventLogEntryTvp entry)
    {
        if (RejectNext)
        {
            RejectNext = false;
            return false;
        }

        Enqueued.Add(entry);
        return true;
    }
}
