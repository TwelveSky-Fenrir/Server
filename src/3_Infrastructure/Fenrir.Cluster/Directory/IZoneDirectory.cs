namespace Fenrir.Cluster.Directory;

public interface IZoneDirectory
{
    public ValueTask<ZoneEndpoint?> ResolveAsync(short zoneId, CancellationToken cancellationToken);

    public ValueTask HeartbeatAsync(ZoneEndpoint endpoint, int currentPlayers, CancellationToken cancellationToken);
}
