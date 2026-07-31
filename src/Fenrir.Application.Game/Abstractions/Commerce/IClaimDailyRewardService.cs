using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface IClaimDailyRewardService
{
    public ValueTask<ClaimDailyRewardResponse?> ResolveAndApplyAsync(ClaimDailyRewardRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken);
}
