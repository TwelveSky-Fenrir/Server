using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class GetCashCatalogService(CommerceCatalogCache catalog, ILogger<GetCashCatalogService> logger)
    : IGetCashCatalogService
{
    public GetCashCatalogResponse GetCatalog(PlayerRuntimeState? state)
    {
        var version = catalog.CashCatalogVersion;

        if (state is not null)
        {
            state.KnownCashCatalogVersion = version;
            logger.LogDebug("Get cash catalog: character {CharacterId} served catalog version {Version}",
                state.CharacterId, version);
        }
        else
        {
            logger.LogDebug("Get cash catalog: served catalog version {Version} (no resolved player yet)", version);
        }

        return new GetCashCatalogResponse
        {
            Result = 0,
            Version = version,
            CashItemInfo = catalog.CashCatalog.DisplayGrid
        };
    }
}
