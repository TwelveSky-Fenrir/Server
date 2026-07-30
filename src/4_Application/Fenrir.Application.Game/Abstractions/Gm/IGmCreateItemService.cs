using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Sessions;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmCreateItemService
{
    public ValueTask HandleAsync(int sort, byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        Zone zone,
        CancellationToken cancellationToken);
}
