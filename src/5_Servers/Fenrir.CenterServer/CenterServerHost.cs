using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.CenterServer;

/// <summary>
/// Service de cycle de vie du CenterServer (Lot F2). Serveur TCP <b>interne et passif</b> : il accepte des
/// connexions serveur-à-serveur (Login, Zones) et n'ouvre aucun TCP sortant de jeu. Au stade squelette, il
/// annonce sa configuration ; le module <c>Fenrir.Cluster</c> (annuaire, relais, world-state) est rempli au Lot F4.
/// </summary>
internal sealed class CenterServerHost(ILogger<CenterServerHost> logger, IOptions<CenterServerOptions> options)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Fenrir.CenterServer (squelette F2) — endpoint TCP interne prévu sur :{Port} (coordination cross-zone, " +
            "passif). Module Cluster (annuaire/relais/world-state) au Lot F4.",
            options.Value.Port);
        return Task.CompletedTask;
    }
}
