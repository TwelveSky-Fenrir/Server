using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeChatCrossShardRelayQueue : IChatCrossShardRelayQueue
{
    public List<ChatCrossShardWhisperEntry> Enqueued { get; } = [];

    public bool EnqueueResult { get; set; } = true;

    public bool Enqueue(ChatCrossShardWhisperEntry entry)
    {
        Enqueued.Add(entry);
        return EnqueueResult;
    }
}
