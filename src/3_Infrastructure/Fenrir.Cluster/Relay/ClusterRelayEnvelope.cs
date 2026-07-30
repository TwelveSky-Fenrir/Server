namespace Fenrir.Cluster.Relay;

public readonly record struct ClusterRelayEnvelope<TEvent>(
    Guid CorrelationId,
    short SourceZone,
    short? TargetZone,
    TEvent Payload)
    where TEvent : notnull;
