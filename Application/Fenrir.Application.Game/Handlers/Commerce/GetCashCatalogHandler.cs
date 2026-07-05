using Fenrir.Application.Game.GameData;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_GET_CASH_ITEM_INFO_SEND (opcode 91) -- the cash-shop catalog. The legacy only replies when the
///     client's cached version differs; Fenrir always replies instead, harmless since the catalog is
///     boot-time-static.
/// </summary>
public sealed class GetCashCatalogHandler(WorldDataCache worldData) : IInlinePacketHandler<GetCashCatalogRequest>
{
    public void Handle(in GetCashCatalogRequest packet, IPacketSession session)
    {
        session.Send(new GetCashCatalogResponse
        {
            Result = 0,
            Version = worldData.CashCatalogVersion,
            CashItemInfo = worldData.CashCatalog.DisplayGrid
        });
    }
}
