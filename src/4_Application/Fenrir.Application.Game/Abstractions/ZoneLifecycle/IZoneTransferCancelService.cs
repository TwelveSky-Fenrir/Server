using Fenrir.Application.Game;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IZoneTransferCancelService
{
    public ValueTask HandleAsync(ZoneClientSession zoneSession, CancellationToken cancellationToken);
}
