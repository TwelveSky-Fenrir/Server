using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface IClaimDailyRewardService
{
    public ValueTask<ClaimDailyRewardResponse?> ResolveAndApplyAsync(ClaimDailyRewardRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken);
}
