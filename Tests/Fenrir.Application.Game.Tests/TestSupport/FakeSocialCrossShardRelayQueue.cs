using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for <see cref="ISocialCrossShardRelayQueue" />: records every enqueued Ask/Answer
///     row (append-only) so the six negotiation *.Services' own WS1.4 cross-shard tests can assert a relay
///     entry was (or was not) queued, without a real channel/background host.
/// </summary>
internal sealed class FakeSocialCrossShardRelayQueue : ISocialCrossShardRelayQueue
{
    public List<SocialCrossShardRelayEntry> Enqueued { get; } = [];

    /// <summary>When true, <see cref="Enqueue" /> reports backpressure (bounded channel full) instead of accepting.</summary>
    public bool RejectNext { get; set; }

    public bool Enqueue(SocialCrossShardRelayEntry entry)
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
