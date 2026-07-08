using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>
///     Business logic for CZ_CLAIM_REWARD_ITEM_SEND (opcode 155), extracted from
///     <see cref="ClaimDailyRewardHandler" />.
/// </summary>
public interface IClaimDailyRewardService
{
    /// <summary>
    ///     Resolves and applies today's daily-reward claim. Returns <c>null</c> when the caller should abort the
    ///     session as faulted; otherwise the response to send back to the client.
    /// </summary>
    public ValueTask<ClaimDailyRewardResponse?> ResolveAndApplyAsync(ClaimDailyRewardRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken);
}
