using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

public interface IGuildTribeBroadcastRelayRepository
{

        public ValueTask PublishAsync(GuildTribeBroadcastRelayEntry entry, CancellationToken ct);

        public ValueTask<ImmutableArray<GuildTribeBroadcastRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct);
}
