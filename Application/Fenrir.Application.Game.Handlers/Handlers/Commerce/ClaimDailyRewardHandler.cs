using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

/// <summary>
///     CZ_CLAIM_REWARD_ITEM_SEND (opcode 155). "Already claimed today" is modeled as a date comparison
///     against game.Characters.RewardClaimDate rather than a per-session flag. Granted quantity is always
///     1 -- the legacy's quantity param is really just a Sort==99 coupon display flag, not a stack size.
/// </summary>
public sealed class ClaimDailyRewardHandler(IClaimDailyRewardService service, ILogger<ClaimDailyRewardHandler> logger)
    : IAsyncPacketHandler<ClaimDailyRewardRequest>
{
    public async ValueTask HandleAsync(ClaimDailyRewardRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug(
            "Session {SessionId}: ClaimDailyRewardRequest (op155) received for character {CharacterId}",
            session.SessionId, characterId);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await service.ResolveAndApplyAsync(packet, zone, state, characterId, cancellationToken);
            if (result is null)
            {
                logger.LogWarning(
                    "Daily-reward claim rejected for character {CharacterId} -- aborting session",
                    characterId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            session.Send(result.Value);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
