using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeChatCrossShardRelayRepository : IChatCrossShardRelayRepository
{
    public List<ChatCrossShardWhisperEntry> Published { get; } = [];

        public List<ChatCrossShardWhisperDto> NextPoll { get; set; } = [];

        public Exception? ThrowOnPublish { get; set; }

    public ValueTask PublishAsync(ChatCrossShardWhisperEntry entry, CancellationToken ct)
    {
        if (ThrowOnPublish is { } ex)
            throw ex;

        Published.Add(entry);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableArray<ChatCrossShardWhisperDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct)
    {
        var result = NextPoll.ToImmutableArray();
        NextPoll = [];
        return ValueTask.FromResult(result);
    }
}
