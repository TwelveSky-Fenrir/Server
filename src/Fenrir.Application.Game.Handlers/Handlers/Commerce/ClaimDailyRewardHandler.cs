using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class ClaimDailyRewardHandler(IClaimDailyRewardService service, ILogger<ClaimDailyRewardHandler> logger)
    : IAsyncPacketHandler<ClaimDailyRewardRequest>
{
    public async ValueTask HandleAsync(ClaimDailyRewardRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        logger.LogDebug(
            "Session {SessionId}: ClaimDailyRewardRequest (op155) received for account {AccountId}, character {CharacterId}",
            session.SessionId, accountId, characterId);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        bool disconnect;
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result =
                await service.ResolveAndApplyAsync(packet, zone, state, accountId, characterId, cancellationToken);
            disconnect = result.Disconnect;
            if (disconnect)
                logger.LogWarning(
                    "Daily-reward claim cycle exhausted for account {AccountId}, character {CharacterId}: disconnecting",
                    accountId, characterId);
            else if (result.Response is null)
                logger.LogWarning(
                    "Daily-reward claim rejected for account {AccountId}, character {CharacterId}",
                    accountId, characterId);
            else
                session.Send(result.Response.Value);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }

        if (disconnect)
            zoneSession.Abort(DisconnectReason.Faulted);
    }
}
