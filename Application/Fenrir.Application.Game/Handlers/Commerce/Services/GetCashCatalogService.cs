using Fenrir.Application.Game.GameData;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Commerce.Services;

/// <summary>Business logic for CZ_GET_CASH_ITEM_INFO_SEND (opcode 91), extracted from <see cref="GetCashCatalogHandler" />.</summary>
public interface IGetCashCatalogService
{
    GetCashCatalogResponse GetCatalog();
}

/// <summary>
///     The legacy only replies when the client's cached version differs; Fenrir always replies instead,
///     harmless since the catalog is boot-time-static.
/// </summary>
public sealed class GetCashCatalogService(WorldDataCache worldData) : IGetCashCatalogService
{
    public GetCashCatalogResponse GetCatalog()
    {
        return new GetCashCatalogResponse
        {
            Result = 0,
            Version = worldData.CashCatalogVersion,
            CashItemInfo = worldData.CashCatalog.DisplayGrid
        };
    }
}
