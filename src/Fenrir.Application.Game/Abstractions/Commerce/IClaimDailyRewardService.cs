using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>
///     Disconnect true means the day-range guard tripped (RewardClaimDay outside 0-6) and the caller must
///     hard-disconnect the session with no response, mirroring legacy's Quit() (Server/ts25zone/S04_MyWork02.cpp:15332-15336).
///     Response null with Disconnect false means a data-availability guard tripped (missing claim-state row,
///     bundle config, or item definition) and the caller sends nothing but keeps the session alive.
/// </summary>
public readonly record struct ClaimDailyRewardResult(bool Disconnect, ClaimDailyRewardResponse? Response);

public interface IClaimDailyRewardService
{
    public ValueTask<ClaimDailyRewardResult> ResolveAndApplyAsync(ClaimDailyRewardRequest packet, Zone zone,
        PlayerRuntimeState state, int accountId, int characterId, CancellationToken cancellationToken);
}
