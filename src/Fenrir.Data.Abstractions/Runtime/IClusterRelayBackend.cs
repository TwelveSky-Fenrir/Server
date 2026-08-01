using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

public interface IClusterRelayBackend<TEntry, TDto>
{
    public ValueTask PublishAsync(TEntry entry, CancellationToken ct);

    public ValueTask<ImmutableArray<TDto>> PollAsync(byte shardId, int retentionSeconds, CancellationToken ct);
}
