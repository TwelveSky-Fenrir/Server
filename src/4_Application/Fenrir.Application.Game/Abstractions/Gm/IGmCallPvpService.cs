using Fenrir.Application.Game;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmCallPvpService
{
    public ValueTask HandleAsync(GmCallPvpPayload packet, byte[] data, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}
