using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmSetPvpPointService
{

        public ValueTask HandleAsync(GmSetPvpPointPayload packet, byte[] data, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}
