namespace Fenrir.Cluster.WorldState;

public interface ICenterPeerRegistry
{
    public int DisconnectIdlePeers(TimeSpan idleThreshold, DateTimeOffset now);
}
