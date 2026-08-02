using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class OpenShopStallHandler(IOpenShopStallService service, ILogger<OpenShopStallHandler> logger)
    : IAsyncPacketHandler<OpenShopStallRequest>
{
    public const short PshopZoneNumber = ProxyShopZonePolicy.ZoneNumber;

    public async ValueTask HandleAsync(OpenShopStallRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        logger.LogDebug(
            "OpenShopStall: session {SessionId} character {CharacterId} sort {Sort} name {ShopName}",
            session.SessionId, characterId, packet.Sort, packet.PshopInfo.Name);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != PshopZoneNumber)
        {
            logger.LogWarning(
                "Open shop stall rejected: character {CharacterId} is outside the market district (zone {MapId})",
                characterId, zone.MapId);
            return;
        }

        var prepared = await service.PrepareAsync(packet, state, cancellationToken);
        switch (prepared.Outcome)
        {
            case OpenShopStallPrepareOutcome.Abort:
                return;
            case OpenShopStallPrepareOutcome.LiveOpened:
            case OpenShopStallPrepareOutcome.Blocked:
                session.Send(prepared.Response!.Value);
                return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var response = await service.OpenProxyShopAsync(packet, zone, state, characterId, accountId,
                prepared.Listing, prepared.OfflineItems!, cancellationToken);
            session.Send(response);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
