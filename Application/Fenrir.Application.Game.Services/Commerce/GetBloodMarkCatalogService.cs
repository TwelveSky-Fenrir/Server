using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class GetBloodMarkCatalogService(WorldDataCache worldData) : IGetBloodMarkCatalogService
{
    public BloodShop GetCatalog()
    {
        return BloodShopBuilder.Build(worldData.BloodExchangeCatalog, worldData.ItemsById);
    }
}
