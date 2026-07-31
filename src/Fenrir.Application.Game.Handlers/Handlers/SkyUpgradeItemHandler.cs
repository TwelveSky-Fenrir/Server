using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class SkyUpgradeItemHandler(
    ISkyUpgradeItemService skyUpgradeItemService,
    ILogger<SkyUpgradeItemHandler> logger)
    : IAsyncPacketHandler<SkyUpgradeItemRequest>
{
    public async ValueTask HandleAsync(SkyUpgradeItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: SkyUpgradeItemRequest received ({Page1}:{Index1} + {Page2}:{Index2})",
                zoneSession.SessionId, characterId, packet.Page1, packet.Index1, packet.Page2, packet.Index2);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
        {
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: SkyUpgradeItemRequest dropped, no live zone/player state",
                zoneSession.SessionId, characterId);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await skyUpgradeItemService.UpgradeAsync(packet, zone, state, characterId,
                cancellationToken);

            if (result.Outcome != SkyUpgradeItemOutcome.Applied)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            session.Send(new SkyUpgradeItemResponse
            {
                Result = result.Succeeded ? 0 : 1,
                Cost = SkyUpgradeResolver.Cost,
                Value = result.Value
            });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
