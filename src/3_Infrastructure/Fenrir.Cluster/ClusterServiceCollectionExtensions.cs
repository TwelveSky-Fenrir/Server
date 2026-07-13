using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Cluster;

/// <summary>
/// Enregistrement DI du module <c>Fenrir.Cluster</c> (backbone de coordination cross-zone, ex-<c>ts25center</c>).
/// Squelette : les implémentations concrètes (annuaire de zones, tickets, relais consolidés, coordination
/// world-state autoritaire) sont ajoutées au Lot F4.
/// </summary>
public static class ClusterServiceCollectionExtensions
{
    /// <summary>Enregistre les services du module Cluster (annuaire, sessions/tickets, relais).</summary>
    public static IServiceCollection AddFenrirCluster(this IServiceCollection services)
    {
        // TODO(F4) : AddSingleton<IZoneDirectory, ...>, <IHandoverTicketService, ...>, relais consolidés
        //            (Publish/Poll + push TCP), coordination du world-state (hero-rank/tribu/tour/party).
        return services;
    }
}
