using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op26, CZ_EXCHANGE_ITEM_SEND -- rerolls a Rare/Elite equip item into a random same-tier/category
///     replacement (delegated to <see cref="IRerollItemService" />). <c>Sort</c>/<c>Value1</c>/<c>Value2</c> are
///     dead wire fields the legacy handler itself never reads.
/// </summary>
public sealed class RerollItemHandler(IRerollItemService rerollItemService, ILogger<RerollItemHandler> logger)
    : IAsyncPacketHandler<RerollItemRequest>
{
    public async ValueTask HandleAsync(RerollItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: RerollItemRequest received ({Page1}:{Index1})",
                zoneSession.SessionId, characterId, packet.Page1, packet.Index1);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
        {
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: RerollItemRequest dropped, no live zone/player state",
                zoneSession.SessionId, characterId);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await rerollItemService.RerollAsync(packet, zone, state, characterId, cancellationToken);

            switch (result.Outcome)
            {
                case RerollItemOutcome.Rejected:
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
                case RerollItemOutcome.NoCandidate:
                    session.Send(new RerollItemResponse { Result = 1, Cost = result.Cost, Value = result.Value });
                    return;
            }

            session.Send(new RerollItemResponse { Result = 0, Cost = result.Cost, Value = result.Value });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
