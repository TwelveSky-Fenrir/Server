using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmCreateItemService
{
    public ValueTask HandleAsync(int sort, byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        Zone zone,
        CancellationToken cancellationToken);
}
