using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IZoneTransferCancelService
{
    public ValueTask HandleAsync(ZoneClientSession zoneSession, CancellationToken cancellationToken);
}
