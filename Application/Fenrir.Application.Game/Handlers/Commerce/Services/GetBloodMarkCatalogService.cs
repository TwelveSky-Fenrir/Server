using Fenrir.Application.Game.Commerce;
using Fenrir.Application.Game.GameData;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Handlers.Commerce.Services;

/// <summary>Business logic for CZ_DEMAND_BLOOD_MARK_SEND (opcode 140), extracted from <see cref="GetBloodMarkCatalogHandler" />.</summary>
public interface IGetBloodMarkCatalogService
{
    BloodShop GetCatalog();
}

public sealed class GetBloodMarkCatalogService(WorldDataCache worldData) : IGetBloodMarkCatalogService
{
    public BloodShop GetCatalog()
    {
        return BloodShopBuilder.Build(worldData.BloodExchangeCatalog, worldData.ItemsById);
    }
}
