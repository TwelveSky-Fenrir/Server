using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for <see cref="IGuildBuffExpiryRelayQueue" />: records every enqueued
///     guild-buff-reserve-exhaustion push (append-only) so <c>GuildBuffDecayHost</c>'s own tests can assert a
///     cross-shard relay entry was (or was not) queued, without a real channel/background host.
/// </summary>
internal sealed class FakeGuildBuffExpiryRelayQueue : IGuildBuffExpiryRelayQueue
{
    public List<GuildBuffExpiryRelayEntry> Enqueued { get; } = [];

    public bool Enqueue(GuildBuffExpiryRelayEntry entry)
    {
        Enqueued.Add(entry);
        return true;
    }
}
