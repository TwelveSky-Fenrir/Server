using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmExpGrantService
{
    public ValueTask HandleAsync(GmExpGrantPayload packet, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken);
}
