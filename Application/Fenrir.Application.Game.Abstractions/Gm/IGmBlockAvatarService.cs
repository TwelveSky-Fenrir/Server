using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmBlockAvatarService
{
    public ValueTask HandleAsync(GmBlockAvatarPayload packet, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}
