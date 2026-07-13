using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

public interface IRvrSiegeEventRelayRepository
{
    public ValueTask PublishAsync(RvrSiegeEventRelayEntry entry, CancellationToken ct);

    public ValueTask<ImmutableArray<RvrSiegeEventRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct);
}
