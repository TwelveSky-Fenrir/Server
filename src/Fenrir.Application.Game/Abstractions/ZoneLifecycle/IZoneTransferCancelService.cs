using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IZoneTransferCancelService
{
    public ValueTask HandleAsync(IZoneSession zoneSession, CancellationToken cancellationToken);
}
