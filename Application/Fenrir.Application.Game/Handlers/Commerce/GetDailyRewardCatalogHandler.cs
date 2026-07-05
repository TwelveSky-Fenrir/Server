using Fenrir.Application.Game.Handlers.Commerce.Services;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_GET_REWARD_ITEM_SEND (opcode 154) -- the 7-day login-reward catalog plus this character's claim
///     cursor.
/// </summary>
public sealed class GetDailyRewardCatalogHandler(IGetDailyRewardCatalogService service)
    : IAsyncPacketHandler<GetDailyRewardCatalogRequest>
{
    public async ValueTask HandleAsync(GetDailyRewardCatalogRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        var response = await service.GetCatalogAsync(characterId, cancellationToken);

        session.Send(response);
    }
}
