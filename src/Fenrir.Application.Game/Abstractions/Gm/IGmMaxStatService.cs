using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmMaxStatService
{
    public ValueTask HandleAsync(IZoneSession zoneSession, PlayerRuntimeState state, Zone zone,
        CancellationToken cancellationToken);
}
