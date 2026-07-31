using Fenrir.Application.Game.Sessions;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IEnterWorldService
{
    public ValueTask HandleAsync(EnterWorldRequest packet, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}
