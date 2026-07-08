using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op89, CZ_DESTROY_ITEM_SEND -- dissolves an enchanted Rare equip item into money plus a compensation
///     stone (delegated to <see cref="IDestroyItemService" />). Only the LNW33 (EU33) branch is reproduced
///     (Elite items are always rejected in this build, see <c>DestroyResolver</c>'s remarks).
/// </summary>
public sealed class DestroyItemHandler(IDestroyItemService destroyItemService, ILogger<DestroyItemHandler> logger)
    : IAsyncPacketHandler<DestroyItemRequest>
{
    public async ValueTask HandleAsync(DestroyItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
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
