using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmMaxStatService
{
    public ValueTask HandleAsync(ZoneClientSession zoneSession, PlayerRuntimeState state, Zone zone,
        CancellationToken cancellationToken);
}
