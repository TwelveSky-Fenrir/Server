namespace Fenrir.Cluster.Relay;

public interface IClusterRelay<TEvent>
    where TEvent : notnull
{
    public ValueTask PublishAsync(ClusterRelayEnvelope<TEvent> envelope, CancellationToken cancellationToken);
}

public interface IClusterRelayHandler<TEvent>
    where TEvent : notnull
{
    public ValueTask HandleAsync(ClusterRelayEnvelope<TEvent> envelope, CancellationToken cancellationToken);
}
