using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Core.Packets.Shared;
using Fenrir.Domain.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class GetBloodMarkCatalogService(
    CommerceCatalogCache catalog,
    WorldDataCache worldData,
    ILogger<GetBloodMarkCatalogService> logger) : IGetBloodMarkCatalogService
{
    public BloodShop GetCatalog()
    {
        var shop = BloodShopBuilder.Build(catalog.BloodExchangeCatalog, worldData.ItemsById);
        logger.LogDebug("Get blood mark catalog: served {EntryCount} entries", shop.BloodNum);
        return shop;
    }
}
