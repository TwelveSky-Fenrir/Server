using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmSetPvpPointService
{
    public ValueTask HandleAsync(GmSetPvpPointPayload packet, byte[] data, IZoneSession zoneSession,
        CancellationToken cancellationToken);
}
