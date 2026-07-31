namespace Fenrir.Cluster.Center.WorldState;

public interface ICenterPeerRegistry
{
    public int DisconnectIdlePeers(TimeSpan idleThreshold, DateTimeOffset now);
}
