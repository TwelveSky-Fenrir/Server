using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class DestroyItemHandler(IDestroyItemService destroyItemService, ILogger<DestroyItemHandler> logger)
    : IAsyncPacketHandler<DestroyItemRequest>
{
    public async ValueTask HandleAsync(DestroyItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: DestroyItemRequest received ({Page1}:{Index1})",
                zoneSession.SessionId, characterId, packet.Page1, packet.Index1);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
        {
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: DestroyItemRequest dropped, no live zone/player state",
                zoneSession.SessionId, characterId);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await destroyItemService.DestroyAsync(packet, zone, state, characterId, accountId,
                cancellationToken);

            if (result.Outcome != DestroyItemOutcome.Applied)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            session.Send(new DestroyItemResponse
            {
                Result = 0, Money = result.Money,
                Value = [result.StoneItemId, 0, 0, result.Quantity, 0, result.Serial]
            });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
