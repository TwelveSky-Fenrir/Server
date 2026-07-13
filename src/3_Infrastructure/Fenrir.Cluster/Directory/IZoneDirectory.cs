using System.Threading;
using System.Threading.Tasks;

namespace Fenrir.Cluster.Directory;

public interface IZoneDirectory
{

        ValueTask<ZoneEndpoint?> ResolveAsync(short zoneId, CancellationToken cancellationToken);

        ValueTask HeartbeatAsync(ZoneEndpoint endpoint, int currentPlayers, CancellationToken cancellationToken);
}
