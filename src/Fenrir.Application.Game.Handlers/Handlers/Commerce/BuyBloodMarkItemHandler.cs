using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class BuyBloodMarkItemHandler(
    IBuyBloodMarkItemService service,
    ILogger<BuyBloodMarkItemHandler> logger) : IAsyncPacketHandler<BuyBloodMarkItemRequest>
{
    public async ValueTask HandleAsync(BuyBloodMarkItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug(
            "BuyBloodMarkItem: session {SessionId} character {CharacterId} bloodIndex {BloodIndex} slot {Page}/{Index}",
            session.SessionId, characterId, packet.BloodIndex, packet.Page, packet.Index);

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
                    "Buy blood mark item rejected: character {CharacterId} request failed structural validation -- session will be disconnected",
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
