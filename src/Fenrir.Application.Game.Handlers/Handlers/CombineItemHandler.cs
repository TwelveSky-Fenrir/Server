using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class CombineItemHandler(ICombineItemService combineItemService, ILogger<CombineItemHandler> logger)
    : IAsyncPacketHandler<CombineItemRequest>
{
    public async ValueTask HandleAsync(CombineItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: CombineItemRequest received ({Page1}:{Index1} + {Page2}:{Index2})",
                zoneSession.SessionId, characterId, packet.Page1, packet.Index1, packet.Page2, packet.Index2);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
        {
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: CombineItemRequest dropped, no live zone/player state",
                zoneSession.SessionId, characterId);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await combineItemService.CombineAsync(packet, zone, state, characterId, cancellationToken);

            if (result.Outcome != CombineItemOutcome.Applied)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            session.Send(new CombineItemResponse { Result = result.ResultCode, Cost = result.Cost });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
