using Fenrir.Application.Game.Sessions;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IZoneMoveService
{
    public ValueTask HandleAsync(ZoneMoveRequest packet, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}
