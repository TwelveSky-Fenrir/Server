using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Proactively tells a client its cached cash-catalog is stale: for every player already in this zone
///     (so past registration and never mid zone-transfer -- see <see cref="Zone.Players" />'s own contract),
///     compares <see cref="PlayerRuntimeState.KnownCashCatalogVersion" /> against the live
///     <see cref="CommerceCatalogCache.CashCatalogVersion" />. A mismatch resets the player's remembered
///     version back to <see cref="PlayerRuntimeState.CashCatalogVersionUnknown" /> and sends the empty
///     <see cref="CashCatalogInvalidatedResponse" /> signal -- never the catalog contents themselves, only
///     "come ask again." A session whose remembered version is still the "never fetched" sentinel is
///     deliberately skipped (it will simply receive the current catalog whenever it first asks), and a
///     session already reset by a previous firing is deliberately not re-notified until it explicitly
///     re-requests the catalog and records a real version again.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:1963-2006 (per-session per-tick loop; ready-state/
///     registration-timeout/zone-transfer gating at lines 1966-1990/2001-2005, the version check and reset-
///     plus-notify at 1991-1999) ; Server/Header/Protocol/ZONE.h:1036-1046/1609-1612 and
///     Server/ts25zone/S05_MyTransfer.cpp:1362-1373 (the invalidation-notify wire routine writes no payload)
///     ; Server/BuildEU33/ServerInfo.ini:135-143 (the connection-owning process's 500 ms logic tick that
///     drives this loop in production -- twice <see cref="CommerceCatalogCache" />'s own ~1 s reload cadence).
///     This system runs on every legacy tick (each 500 ms, see <see cref="ISimulationSystem.Simulate" />'s own
///     remarks), matching that cadence with no extra gating needed.
/// </remarks>
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
