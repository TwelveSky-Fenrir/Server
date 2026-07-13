using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IEnterWorldService
{
    public ValueTask HandleAsync(EnterWorldRequest packet, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}
