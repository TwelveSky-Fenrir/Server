namespace Fenrir.Data.Abstractions.Runtime;

public interface IChatCrossShardRelayQueue
{
    public bool Enqueue(ChatCrossShardWhisperEntry entry);
}
