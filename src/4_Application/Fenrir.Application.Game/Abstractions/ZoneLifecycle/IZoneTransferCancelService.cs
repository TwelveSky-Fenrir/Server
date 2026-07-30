using Fenrir.Application.Game.Sessions;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IZoneTransferCancelService
{
    public ValueTask HandleAsync(ZoneClientSession zoneSession, CancellationToken cancellationToken);
}
