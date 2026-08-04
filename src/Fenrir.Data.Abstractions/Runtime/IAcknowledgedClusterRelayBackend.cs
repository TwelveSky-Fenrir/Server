namespace Fenrir.Data.Abstractions.Runtime;

public interface IAcknowledgedClusterRelayBackend<TEntry, TDto> : IClusterRelayBackend<TEntry, TDto>
{
    public ValueTask AcknowledgeAsync(byte shardId, long relayId, CancellationToken ct);
}
