using Fenrir.Cluster.Directory;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Cluster;

/// <summary>
/// Enregistrement DI du module <c>Fenrir.Cluster</c> (backbone de coordination cross-zone, ex-<c>ts25center</c>).
/// À appeler <b>après</b> <c>AddFenrirData()</c> : <see cref="ZoneDirectory"/> compose les repos d'annuaire runtime
/// (<c>IGameServerDirectoryRepository</c> + <c>IShardMapAssignmentRepository</c>) enregistrés par <c>AddFenrirData</c>.
/// </summary>
public static class ClusterServiceCollectionExtensions
{
    /// <summary>Enregistre les services du module Cluster (annuaire de zones ; sessions/tickets, relais consolidés,
    /// world-state autoritaire = Lots F4 ultérieurs).</summary>
    public static IServiceCollection AddFenrirCluster(this IServiceCollection services)
    {
        services.AddSingleton<IZoneDirectory, ZoneDirectory>();
        // TODO(F4) : <IHandoverTicketService, ...>, relais consolidés (Publish/Poll + push TCP), coordination
        //            du world-state (hero-rank/tribu/tour/party).
        return services;
    }
}
