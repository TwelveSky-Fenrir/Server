using System.Threading;
using System.Threading.Tasks;

namespace Fenrir.Cluster.Directory;

/// <summary>
/// Annuaire des zones du cluster : résolution <c>zoneId → (host, port)</c> pour la redirection client, et
/// heartbeat des GameServers (~5 s). C'est ce que le LoginServer interroge pour renvoyer au client l'ip:port de
/// la zone cible (jadis lu en SHM, <c>ts25login/S04_MyWork02.cpp:1588-1589</c>).
/// </summary>
public interface IZoneDirectory
{
    /// <summary>Résout l'endpoint public d'une zone ; <c>null</c> si la zone n'est pas en ligne.</summary>
    ValueTask<ZoneEndpoint?> ResolveAsync(short zoneId, CancellationToken cancellationToken);

    /// <summary>Publie/rafraîchit la présence d'une zone (host:port + charge courante).</summary>
    ValueTask HeartbeatAsync(ZoneEndpoint endpoint, int currentPlayers, CancellationToken cancellationToken);
}
