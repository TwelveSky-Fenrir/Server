using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmSummonMonsterService
{
    public ValueTask HandleAsync(GmSummonMonsterPayload packet, byte[] data, IZoneSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken);
}
