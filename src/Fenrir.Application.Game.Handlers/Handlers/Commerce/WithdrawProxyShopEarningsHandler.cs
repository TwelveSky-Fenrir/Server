using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class WithdrawProxyShopEarningsHandler(
    IWithdrawProxyShopEarningsService service,
    ILogger<WithdrawProxyShopEarningsHandler> logger) : IAsyncPacketHandler<WithdrawProxyShopEarningsRequest>
{
    public async ValueTask HandleAsync(WithdrawProxyShopEarningsRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        logger.LogDebug(
            "WithdrawProxyShopEarnings: session {SessionId} character {CharacterId} money {Money} bigMoney {BigMoney}",
            session.SessionId, characterId, packet.Money, packet.BigMoney);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != OpenShopStallHandler.PshopZoneNumber)
        {
            logger.LogWarning(
                "Withdraw proxy shop earnings rejected: character {CharacterId} is outside the market district (zone {MapId}) -- session will be disconnected",
                characterId, zone.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var response = await service.WithdrawAsync(characterId, accountId, packet.Money, packet.BigMoney,
                cancellationToken);
            session.Send(response);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
