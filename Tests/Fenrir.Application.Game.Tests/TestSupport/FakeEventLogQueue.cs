using Fenrir.Data.Abstractions.Game;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeEventLogQueue : IEventLogQueue
{
    public List<EventLogEntryTvp> Enqueued { get; } = [];

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
