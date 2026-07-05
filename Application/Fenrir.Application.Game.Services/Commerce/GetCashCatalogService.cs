using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Services.Commerce;

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
