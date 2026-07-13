using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Packets.Zone;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class CashCatalogStaleNotifySystem(CommerceCatalogCache catalog) : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var currentVersion = catalog.CashCatalogVersion;

        foreach (var state in zone.Players)
        {
            if (state.KnownCashCatalogVersion == PlayerRuntimeState.CashCatalogVersionUnknown)
                continue;

            if (state.KnownCashCatalogVersion == currentVersion)
                continue;

            state.KnownCashCatalogVersion = PlayerRuntimeState.CashCatalogVersionUnknown;
            state.Session.Send(new CashCatalogInvalidatedResponse());
        }
    }
}
