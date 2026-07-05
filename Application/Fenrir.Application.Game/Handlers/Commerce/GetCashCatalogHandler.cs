using Fenrir.Application.Game.GameData;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Application.Game.Handlers.Commerce.Services;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_GET_CASH_ITEM_INFO_SEND (opcode 91) -- the cash-shop catalog. The legacy only replies when the
///     client's cached version differs; Fenrir always replies instead, harmless since the catalog is
///     boot-time-static.
/// </summary>
public sealed class GetCashCatalogHandler(IGetCashCatalogService service) : IInlinePacketHandler<GetCashCatalogRequest>
{
    public void Handle(in GetCashCatalogRequest packet, IPacketSession session)
    {
        session.Send(service.GetCatalog());
    }
}
