using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmClearInventoryService
{

        public ValueTask HandleAsync(GmClearInventoryPayload packet, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken);
}
