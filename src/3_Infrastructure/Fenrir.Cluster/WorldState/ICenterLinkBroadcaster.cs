namespace Fenrir.Cluster.WorldState;

public interface ICenterLinkBroadcaster
{
    public ValueTask BroadcastWorldEventAsync(int sort, ReadOnlyMemory<byte> data, CancellationToken ct);
}
