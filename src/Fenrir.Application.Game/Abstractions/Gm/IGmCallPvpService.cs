using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmCallPvpService
{
    public ValueTask HandleAsync(GmCallPvpPayload packet, byte[] data, IZoneSession zoneSession,
        CancellationToken cancellationToken);
}
