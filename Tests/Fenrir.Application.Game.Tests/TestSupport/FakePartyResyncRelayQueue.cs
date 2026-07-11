using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakePartyResyncRelayQueue : IPartyResyncRelayQueue
{
    public List<PartyResyncRelayEntry> Enqueued { get; } = [];

    public bool RejectNext { get; set; }

    public bool Enqueue(PartyResyncRelayEntry entry)
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
