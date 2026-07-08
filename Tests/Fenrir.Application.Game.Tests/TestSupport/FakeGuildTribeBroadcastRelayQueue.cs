using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for IGuildTribeBroadcastRelayQueue: records every enqueued cross-shard broadcast
///     (append-only) so GuildAnnouncement/GuildChat/TribeAnnouncement/TribeAnnouncementScroll service test
///     suites can assert a relay entry was (or was not) queued, without a real channel/background host.
/// </summary>
internal sealed class FakeGuildTribeBroadcastRelayQueue : IGuildTribeBroadcastRelayQueue
{
    public List<GuildTribeBroadcastRelayEntry> Enqueued { get; } = [];

    /// <summary>When true, <see cref="Enqueue" /> reports backpressure (bounded channel full) instead of accepting.</summary>
    public bool RejectNext { get; set; }

    public bool Enqueue(GuildTribeBroadcastRelayEntry entry)
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
