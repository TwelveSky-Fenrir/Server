using Fenrir.Application.Game.GameData;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_GET_CASH_ITEM_INFO_SEND (opcode 91, contracts/04_commerce.md) -- the cash-shop catalog (12 809
///     bytes, the largest packet in the protocol). The legacy only replies when the client's own cached
///     <c>mCashVersion</c> differs from <c>mCashInfo-&gt;mVersion</c> (silence otherwise) -- Fenrir does
///     not track a per-session "last sent version" (the catalog is boot-time-static in this pass, so the
///     optimization only ever mattered once per session anyway): this handler ALWAYS replies, a
///     deliberate, documented simplification that is harmless (an extra, byte-identical 12 809-byte reply
///     the client would have re-requested regardless) rather than silently risking a client stuck without
///     its first catalog fetch.
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
