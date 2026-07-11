using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class BuyCashItemHandler(IBuyCashItemService service, ILogger<BuyCashItemHandler> logger)
    : IAsyncPacketHandler<BuyCashItemRequest>
{
    public async ValueTask HandleAsync(BuyCashItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        logger.LogDebug(
            "BuyCashItem: session {SessionId} character {CharacterId} costInfoIndex {CostInfoIndex} slot {Page}/{Index}",
            session.SessionId, characterId, packet.CostInfoIndex, packet.Page, packet.Index);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await service.ResolveAndApplyAsync(packet, zone, state, characterId, accountId,
                cancellationToken);
            if (result is null)
            {
                logger.LogWarning(
                    "Buy cash item rejected: character {CharacterId} request failed structural validation -- session will be disconnected",
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
