using Fenrir.Application.Game.Commerce;
using Fenrir.Application.Game.GameData;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_GET_CASH_ITEM_INFO_SEND (opcode 91, contracts/04_commerce.md) -- the cash-shop catalog (12,809
///     bytes, the largest packet in the protocol). The legacy only replies when the client's cached
///     <c>mCashVersion</c> differs from the server's (silence otherwise); Fenrir doesn't track a
///     per-session "last sent version" and always replies instead -- harmless since the catalog is
///     boot-time-static in this pass (an extra byte-identical reply the client would re-request anyway).
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
