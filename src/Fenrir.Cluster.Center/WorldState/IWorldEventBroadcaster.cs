namespace Fenrir.Cluster.Center.WorldState;

public interface IWorldEventBroadcaster
{
    public ValueTask BroadcastWorldEventAsync(int sort, ReadOnlyMemory<byte> data, CancellationToken ct);
}
