using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeSocialCrossShardRelayQueue : ISocialCrossShardRelayQueue
{
    public List<SocialCrossShardRelayEntry> Enqueued { get; } = [];

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
