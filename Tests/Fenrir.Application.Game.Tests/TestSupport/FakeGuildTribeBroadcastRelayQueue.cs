using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeGuildTribeBroadcastRelayQueue : IGuildTribeBroadcastRelayQueue
{
    public List<GuildTribeBroadcastRelayEntry> Enqueued { get; } = [];

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
