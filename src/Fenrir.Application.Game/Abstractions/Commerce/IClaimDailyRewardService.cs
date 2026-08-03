using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public readonly record struct ClaimDailyRewardResult(bool Disconnect, ClaimDailyRewardResponse? Response);

public interface IClaimDailyRewardService
{
    public ValueTask<ClaimDailyRewardResult> ResolveAndApplyAsync(ClaimDailyRewardRequest packet, Zone zone,
        PlayerRuntimeState state, int accountId, int characterId, CancellationToken cancellationToken);
}
