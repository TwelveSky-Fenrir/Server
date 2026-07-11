using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for <see cref="IChatCrossShardRelayQueue" /> -- records every enqueued whisper so
///     <see cref="Fenrir.Application.Game.Services.Chat.WhisperService" />'s cross-shard-miss branch can be
///     asserted without the real <c>ChatCrossShardRelayHost</c>. <see cref="EnqueueResult" /> lets a test
///     simulate a full outbox (returns false).
/// </summary>
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
