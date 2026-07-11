using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for <see cref="IPartyResyncRelayQueue" />: records every enqueued row (append-only)
///     so <c>PartyResyncRelayHandler</c>'s own tests can assert a reconciliation result was (or was not)
///     republished, without a real channel/background host. Same shape as
///     <see cref="FakeSocialCrossShardRelayQueue" />.
/// </summary>
internal sealed class FakePartyResyncRelayQueue : IPartyResyncRelayQueue
{
    public List<PartyResyncRelayEntry> Enqueued { get; } = [];

    /// <summary>When true, <see cref="Enqueue" /> reports backpressure (bounded channel full) instead of accepting.</summary>
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
