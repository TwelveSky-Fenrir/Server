using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

public interface IChatCrossShardRelayRepository
{

        public ValueTask PublishAsync(ChatCrossShardWhisperEntry entry, CancellationToken ct);

        public ValueTask<ImmutableArray<ChatCrossShardWhisperDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct);
}
