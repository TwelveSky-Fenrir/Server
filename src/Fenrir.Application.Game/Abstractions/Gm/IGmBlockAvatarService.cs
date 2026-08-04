using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmBlockAvatarService
{
    public ValueTask HandleAsync(GmBlockAvatarPayload packet, IZoneSession zoneSession,
        Zone zone, CancellationToken cancellationToken);
}
