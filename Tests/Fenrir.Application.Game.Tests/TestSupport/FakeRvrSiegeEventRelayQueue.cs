using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for IRvrSiegeEventRelayQueue: records every enqueued cross-shard rvr-siege relay
///     entry (append-only) so ZoneCenterBroadcastIngestor's/ZoneEventBroadcaster's own relay-enqueue tests can
///     assert an entry was (or was not) queued, without a real channel/background host.
/// </summary>
internal sealed class FakeRvrSiegeEventRelayQueue : IRvrSiegeEventRelayQueue
{
    public List<RvrSiegeEventRelayEntry> Enqueued { get; } = [];

    public bool Enqueue(RvrSiegeEventRelayEntry entry)
    {
        Enqueued.Add(entry);
        return true;
    }
}
