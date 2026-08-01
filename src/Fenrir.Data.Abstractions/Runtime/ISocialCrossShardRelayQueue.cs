namespace Fenrir.Data.Abstractions.Runtime;

public interface ISocialCrossShardRelayQueue
{
    public bool Enqueue(SocialCrossShardRelayEntry entry);
}
