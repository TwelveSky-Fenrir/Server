using Fenrir.Application.Game.Sessions;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmSetPvpPointService
{
    public ValueTask HandleAsync(GmSetPvpPointPayload packet, byte[] data, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}
