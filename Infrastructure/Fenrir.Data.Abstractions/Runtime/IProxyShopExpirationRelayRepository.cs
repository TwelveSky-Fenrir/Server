using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

public interface IProxyShopExpirationRelayRepository
{
    public ValueTask PublishAsync(ProxyShopExpirationRelayEntry entry, CancellationToken ct);

    public ValueTask<ImmutableArray<ProxyShopExpirationRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct);
}
