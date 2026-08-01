using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmFfaEventStartService
{
    public ValueTask HandleAsync(GmFfaEventStartPayload packet, byte[] data, IZoneSession zoneSession,
        CancellationToken cancellationToken);
}
