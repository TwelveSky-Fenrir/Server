using Fenrir.Application.Game.Sessions;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmBlockAvatarService
{
    public ValueTask HandleAsync(GmBlockAvatarPayload packet, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}
