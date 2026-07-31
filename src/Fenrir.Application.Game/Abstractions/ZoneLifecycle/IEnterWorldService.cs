using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IEnterWorldService
{
    public ValueTask HandleAsync(EnterWorldRequest packet, IZoneSession zoneSession,
        CancellationToken cancellationToken);
}
