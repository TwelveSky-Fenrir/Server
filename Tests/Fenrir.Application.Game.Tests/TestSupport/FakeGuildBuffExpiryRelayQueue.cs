using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeGuildBuffExpiryRelayQueue : IGuildBuffExpiryRelayQueue
{
    public List<GuildBuffExpiryRelayEntry> Enqueued { get; } = [];

    public bool Enqueue(GuildBuffExpiryRelayEntry entry)
    {
        Enqueued.Add(entry);
        return true;
    }
}
