using System.Threading;
using System.Threading.Tasks;

namespace Fenrir.Cluster.Relay;

public interface IClusterRelay<TEvent>
    where TEvent : notnull
{

        ValueTask PublishAsync(ClusterRelayEnvelope<TEvent> envelope, CancellationToken cancellationToken);
}

public interface IClusterRelayHandler<TEvent>
    where TEvent : notnull
{

        ValueTask HandleAsync(ClusterRelayEnvelope<TEvent> envelope, CancellationToken cancellationToken);
}
