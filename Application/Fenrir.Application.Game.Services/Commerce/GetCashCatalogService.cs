using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

/// <summary>
///     Legacy only replies when the client's cached version differs; Fenrir always replies with the current
///     catalog instead (harmless -- the response's own <c>Version</c> field is authoritative either way), but
///     still records <see cref="PlayerRuntimeState.KnownCashCatalogVersion" /> so
///     <see cref="Fenrir.Application.Game.Domain.Simulation.CashCatalogStaleNotifySystem" />'s own
///     proactive-notify bookkeeping (reset-after-notify, fresh-session exclusion) stays correct.
/// </summary>
/// <remarks>Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:12796-12815 (GET_CASH_ITEM_INFO_SEND handler).</remarks>
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
