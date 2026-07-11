using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

public interface IGuildBuffExpiryRelayRepository
{

        public ValueTask PublishAsync(GuildBuffExpiryRelayEntry entry, CancellationToken ct);

        public ValueTask<ImmutableArray<GuildBuffExpiryRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct);
}
