using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class EnchantItemHandler(IEnchantItemService enchantItemService, ILogger<EnchantItemHandler> logger)
    : IAsyncPacketHandler<EnchantItemRequest>
{
    public async ValueTask HandleAsync(EnchantItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: EnchantItemRequest received ({Page1}:{Index1} + {Page2}:{Index2})",
                zoneSession.SessionId, characterId, packet.Page1, packet.Index1, packet.Page2, packet.Index2);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
        {
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: EnchantItemRequest dropped, no live zone/player state",
                zoneSession.SessionId, characterId);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await enchantItemService.EnchantAsync(packet, zone, state, characterId, cancellationToken);

            if (result.Outcome == EnchantItemOutcome.Rejected)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            session.Send(new EnchantItemResponse
            {
                Result = result.ResultCode, Cost = result.Cost, Value = result.NewEnchant
            });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
