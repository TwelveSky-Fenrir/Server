using Fenrir.Application.Game.Sessions;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmFfaEventStartService
{
    public ValueTask HandleAsync(GmFfaEventStartPayload packet, byte[] data, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}
