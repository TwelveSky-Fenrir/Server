using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", true);

var centerSharedSecret = builder.AddParameter("center-shared-secret", true);

// Plan d'adressage des zones : figé par le client legacy (zone N -> zoneBasePort + N). Le bloc
// zoneBasePort+1 .. zoneBasePort+maxZoneNumber est donc RÉSERVÉ aux listeners de zone (~1101-1449 pour le
// catalogue world.Zones actuel, dont le plus grand ZoneNumber vaut 349) et aucune autre ressource de la
// topologie ne doit y atterrir.
const int zoneBasePort = 1100;

// SQL est déplacé hors du bloc de zones, jamais l'inverse : 1100+mapId n'est pas négociable côté client,
// alors que le port hôte de SQL est libre. Le port SQL conventionnel 1433 correspondrait à mapId 333 (aucune
// map 333 dans world.Zones aujourd'hui, mais le catalogue peut en gagner une), et un port hôte laissé
// aléatoire peut lui aussi tomber dans le bloc. On le fige donc explicitement, hors bloc.
const int sqlHostPort = 14330;

var gamePublicHost = Environment.GetEnvironmentVariable("FENRIR_PUBLIC_HOST") is { Length: > 0 } configuredHost
    ? configuredHost
    : ResolvePrimaryLanIPv4();
Console.WriteLine(
    $"[Fenrir.AppHost] Game__PublicHost = {gamePublicHost} (override with the FENRIR_PUBLIC_HOST env var). " +
    "Clients receive this host to reach zones; it MUST be reachable from the client machine.");

var sql = builder.AddSqlServer("sqlserver", sqlPassword)
    .WithImageTag("2025-latest")
    .WithHostPort(sqlHostPort)
    .WithDataVolume("fenrir-sql-data")
    .WithLifetime(ContainerLifetime.Persistent);

var fenrirDb = sql.AddDatabase("FenrirDb");

// Le health check de "sqlserver" est CONSERVÉ : c'est un `SELECT 1;` TDS sur master (aucun HTTP), la seule
// sonde de la topologie capable de distinguer « le conteneur tourne » de « SQL accepte des logins ». Celui
// de FenrirDb est retiré : db-migrator est propriétaire du schéma et crée lui-même la base depuis master
// (DatabaseProvisioner), donc faire dépendre le migrateur de la santé de la base qu'il crée serait un
// interblocage. Rien ne doit WaitFor(fenrirDb) — les consommateurs passent par WaitForCompletion(migrator),
// garantie strictement plus forte (schéma appliqué, pas seulement base joignable).
RemoveDefaultHealthCheck(fenrirDb.Resource);

// launchProfileName: null sur les 4 AddProject — sans ça Aspire lit Properties/launchSettings.json du projet
// cible et en dérive des endpoints (applicationUrl HTTP/HTTPS). Aucun de ces 4 Worker n'a de launchSettings
// aujourd'hui, mais un « Run » créé depuis l'IDE en poserait un et ferait apparaître une surface HTTP non
// voulue dans la topologie. On coupe la dérivation à la source.
//
// [POINT 3 — CHANGEMENT ISOLÉ] REVERT = remplacer `.WaitFor(sql)` ci-dessous par `.WaitForStart(fenrirDb)`
// (une seule ligne, la dernière de cette déclaration) si db-migrator reste bloqué en Waiting.
// WaitForStart ne garantit que « le conteneur est Running », pas que TDS accepte un login : au premier
// démarrage à froid SQL Server 2025 dépasse couramment le budget de retry du migrateur (SqlConnectionOpener :
// 10 essais x 3 s). WaitFor(sql) attend en plus le health check `sqlserver_check` conservé ci-dessus.
var migrator = builder.AddProject<Fenrir_Tools_DbMigrator>("db-migrator", launchProfileName: null)
    .WithReference(fenrirDb)
    .WaitFor(sql);

const int centerPort = 12003;
var center = builder.AddProject<Fenrir_CenterServer>("center-server", launchProfileName: null)
    .WithReference(fenrirDb)
    .WaitForCompletion(migrator)
    .WithEndpoint(name: "center-tcp", scheme: "tcp", port: centerPort, targetPort: centerPort, isProxied: false)
    .WithEnvironment("Center__Port", centerPort.ToString())
    .WithEnvironment("Center__SharedSecret", centerSharedSecret);

var centerEndpoint = center.GetEndpoint("center-tcp");

const int loginPort = 29998;
builder.AddProject<Fenrir_LoginServer>("login-server", launchProfileName: null)
    .WithReference(fenrirDb)
    .WithReference(centerEndpoint)
    .WaitForCompletion(migrator)
    .WithEndpoint(name: "login-tcp", scheme: "tcp", port: loginPort, targetPort: loginPort, isProxied: false)
    .WithEnvironment("Login__Port", loginPort.ToString())
    .WithEnvironment("Login__ZoneBasePort", zoneBasePort.ToString())
    .WithEnvironment("Center__Endpoint", centerEndpoint)
    .WithEnvironment("Center__SharedSecret", centerSharedSecret);

// Un shard est une PARTITION DISJOINTE de maps, jamais un replica (ADR-0012) : WithReplicas est INTERDIT
// sur game-shard-NN. Deux répliques bindraient les mêmes ports zone, écriraient la même ligne
// runtime.GameServerDirectory et détiendraient deux copies mutables du même état de zone. Monter en charge
// se fait en ajoutant un id à ce tableau ET en réassignant des maps dans admin.ShardMapAssignments — jamais
// en dupliquant un process.
byte[] shardIds = [1];

foreach (var shardId in shardIds)
{
    // Aspire ne peut pas déclarer les vrais listeners du shard : GameConnectionHost ouvre UN socket par map
    // hébergée sur ZoneBasePort + mapId, et la liste des maps vient de admin.ShardMapAssignments, résolue au
    // boot par le shard lui-même. La topologie ignore délibérément qui possède quelle map, donc ces N ports
    // restent hors dashboard et hors manifeste : écart assumé, compensé par la réservation du bloc
    // zoneBasePort ci-dessus (et par le log « GameServer zone listeners: N armed » côté shard).
    // L'endpoint ci-dessous n'est donc PAS « le » port de zone : c'est le port d'ancrage annoncé par le shard
    // (Game__Port -> runtime.GameServerDirectory, et redirection ZoneMoveResponse sur rejet soft). Il vaut
    // zoneBasePort + shardId, c'est-à-dire le listener de la map dont l'id égale le shardId : il n'est
    // réellement bindé que si ce shard héberge cette map.
    var anchorZonePort = zoneBasePort + shardId;

    builder.AddProject<Fenrir_GameServer>($"game-shard-{shardId:00}", launchProfileName: null)
        .WithReference(fenrirDb)
        .WithReference(centerEndpoint)
        .WaitForCompletion(migrator)
        .WithEndpoint(name: "zone-anchor-tcp", scheme: "tcp", port: anchorZonePort, targetPort: anchorZonePort,
            isProxied: false)
        .WithEnvironment("Game__ShardId", shardId.ToString())
        .WithEnvironment("Game__Port", anchorZonePort.ToString())
        .WithEnvironment("Game__ZoneBasePort", zoneBasePort.ToString())
        .WithEnvironment("Game__PublicHost", gamePublicHost)
        .WithEnvironment("Center__Endpoint", centerEndpoint)
        .WithEnvironment("Center__SharedSecret", centerSharedSecret);
}

builder.Build().Run();
return;

static void RemoveDefaultHealthCheck(IResource resource)
{
    foreach (var annotation in resource.Annotations.OfType<HealthCheckAnnotation>().ToArray())
        resource.Annotations.Remove(annotation);
}

// L'adresse publiée est celle que le CLIENT reçoit pour joindre les zones : une erreur ici est invisible
// côté serveur (tout démarre normalement, seuls les clients échouent à se reconnecter). On énumère donc
// explicitement les NIC au lieu de laisser la table de routage arbitrer : une sonde par socket UDP vers un
// hôte Internet élit l'interface qui sort vers INTERNET, pas celle du LAN client — un adaptateur
// VPN/WSL/Hyper-V/Docker remporte régulièrement l'arbitrage, et sans route par défaut elle retombe
// silencieusement sur 127.0.0.1.
static string ResolvePrimaryLanIPv4()
{
    var candidates = new List<(string Address, string Nic, int Rank)>();

    foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
    {
        if (nic.OperationalStatus != OperationalStatus.Up) continue;
        if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
        if (IsVirtualAdapter(nic)) continue;

        IPInterfaceProperties properties;
        try
        {
            properties = nic.GetIPProperties();
        }
        catch (NetworkInformationException)
        {
            continue;
        }

        var hasGateway = properties.GatewayAddresses.Any(gateway =>
            gateway.Address.AddressFamily == AddressFamily.InterNetwork && !gateway.Address.Equals(IPAddress.Any));

        foreach (var unicast in properties.UnicastAddresses)
        {
            var address = unicast.Address;
            if (address.AddressFamily != AddressFamily.InterNetwork) continue;
            if (IPAddress.IsLoopback(address)) continue;

            var octets = address.GetAddressBytes();
            if (octets[0] == 169 && octets[1] == 254) continue; // APIPA : lien sans DHCP, jamais joignable.

            // Passerelle IPv4 configurée > plage privée > Ethernet. Une IPv4 publique reste éligible (serveur
            // dédié sans NAT) mais perd contre le LAN quand les deux existent.
            var rank = (hasGateway ? 4 : 0)
                       + (IsPrivateLan(octets) ? 2 : 0)
                       + (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 1 : 0);

            candidates.Add((address.ToString(), nic.Name, rank));
        }
    }

    if (candidates.Count == 0)
    {
        Console.WriteLine(
            "[Fenrir.AppHost] No usable LAN IPv4 NIC found; falling back to 127.0.0.1. Set FENRIR_PUBLIC_HOST " +
            "if remote clients must connect.");
        return "127.0.0.1";
    }

    // Départage final par l'adresse : le host publié doit être stable d'un run à l'autre.
    var ordered = candidates
        .OrderByDescending(candidate => candidate.Rank)
        .ThenBy(candidate => candidate.Address, StringComparer.Ordinal)
        .ToArray();

    if (ordered.Length > 1)
        Console.WriteLine(
            "[Fenrir.AppHost] Multiple LAN IPv4 candidates: " +
            string.Join(", ", ordered.Select(candidate => $"{candidate.Address} ({candidate.Nic})")) +
            ". Picking the first; set FENRIR_PUBLIC_HOST to override.");

    return ordered[0].Address;
}

static bool IsPrivateLan(byte[] octets) =>
    octets[0] == 10
    || (octets[0] == 172 && octets[1] is >= 16 and <= 31)
    || (octets[0] == 192 && octets[1] == 168);

static bool IsVirtualAdapter(NetworkInterface nic)
{
    string[] markers =
    [
        "hyper-v", "vethernet", "virtualbox", "vmware", "vmnet", "wsl", "docker", "loopback", "pseudo-interface",
        "tap-", "tunnel", "vpn", "openvpn", "wireguard", "nordlynx", "tailscale", "zerotier", "hamachi", "radmin",
        "bluetooth"
    ];

    return markers.Any(marker =>
        nic.Name.Contains(marker, StringComparison.OrdinalIgnoreCase)
        || nic.Description.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
