using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Services.Commerce;

/// <summary>
///     No client-facing invalidation signal exists for the blood-exchange catalog anywhere in the legacy
///     source -- unlike cash, it is always sent live and in full on every explicit request, so this service
///     has no session/version bookkeeping to do at all.
/// </summary>
/// <remarks>Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:15224-15233 (DEMAND_BLOOD_MARK_SEND handler).</remarks>
public sealed class GetBloodMarkCatalogService(CommerceCatalogCache catalog, WorldDataCache worldData)
    : IGetBloodMarkCatalogService
{
    public BloodShop GetCatalog()
    {
        return BloodShopBuilder.Build(catalog.BloodExchangeCatalog, worldData.ItemsById);
    }
}
