using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

public interface ISocialCrossShardRelayRepository
{
    public ValueTask PublishAsync(SocialCrossShardRelayEntry entry, CancellationToken ct);

    public ValueTask<ImmutableArray<SocialCrossShardRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct);
}
