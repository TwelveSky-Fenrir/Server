using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", true);

const int zoneBasePort = 1100;

// Plus grand ZoneNumber du catalogue world.Zones (seed 007_zones.sql : 117 zones, de 1 a 349). Le bloc
// reserve est donc 1101..1449. Une zone au-dela ferait binder le shard hors de ce que la topologie
// annonce ; le garde de boot le refuse au lieu de le faire en silence.
const int maxZoneNumber = 349;
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

var migrator = builder.AddProject<Fenrir_Tools_DbMigrator>("db-migrator", launchProfileName: null)
    .WithReference(fenrirDb)
    .WaitFor(sql);

const int loginPort = 29998;
builder.AddProject<Fenrir_LoginServer>("login-server", launchProfileName: null)
    .WithReference(fenrirDb)
    .WaitForCompletion(migrator)
    .WithEndpoint(name: "login-tcp", scheme: "tcp", port: loginPort, targetPort: loginPort, isProxied: false)
    .WithEnvironment("Login__Port", loginPort.ToString())
    .WithEnvironment("Login__ZoneBasePort", zoneBasePort.ToString());

// Ports hors du bloc de zones que le shard ne doit jamais tenter de binder. Sans cette variable le bras
// "collisions" de ZonePortRangeGuard recoit une liste vide et ne peut plus lever.
var reservedPorts = string.Join(',', new[] { loginPort, sqlHostPort });

byte[] shardIds = [1];

foreach (var shardId in shardIds)
{
    var anchorZonePort = zoneBasePort + shardId;

    builder.AddProject<Fenrir_GameServer>($"game-shard-{shardId:00}", launchProfileName: null)
        .WithReference(fenrirDb)
        .WaitForCompletion(migrator)
        .WithEndpoint(name: "zone-anchor-tcp", scheme: "tcp", port: anchorZonePort, targetPort: anchorZonePort,
            isProxied: false)
        .WithEnvironment("Game__ShardId", shardId.ToString())
        .WithEnvironment("Game__Port", anchorZonePort.ToString())
        .WithEnvironment("Game__ZoneBasePort", zoneBasePort.ToString())
        // Le bloc que CETTE topologie reserve. Le shard le recroise au boot avec les maps que lui donne
        // admin.ShardMapAssignments et refuse de demarrer s'il devrait binder en dehors -- l'AppHost ne
        // peut pas connaitre la liste des maps (elle vient de la base, indisponible a ce moment), mais il
        // sait quel bloc il a mis de cote.
        .WithEnvironment("Game__ZonePortRangeStart", (zoneBasePort + 1).ToString())
        .WithEnvironment("Game__ZonePortRangeEnd", (zoneBasePort + maxZoneNumber).ToString())
        .WithEnvironment("Game__ReservedPorts", reservedPorts)
        .WithEnvironment("Game__PublicHost", gamePublicHost);
        ;
}

builder.Build().Run();
return;

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
            if (octets[0] == 169 && octets[1] == 254) continue;

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

static bool IsPrivateLan(byte[] octets)
{
    return octets[0] == 10
           || (octets[0] == 172 && octets[1] is >= 16 and <= 31)
           || (octets[0] == 192 && octets[1] == 168);
}

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
