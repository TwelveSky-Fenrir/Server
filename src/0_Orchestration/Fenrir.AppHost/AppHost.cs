using System.Net;
using System.Net.Sockets;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", true);

var centerSharedSecret = builder.AddParameter("center-shared-secret", true);

// Hôte public annoncé aux clients pour joindre les zones. Le client (game) se connecte à CET host:port pour entrer
// en zone ; il DOIT être joignable DEPUIS LA MACHINE DU CLIENT. 127.0.0.1 ne marche que si le client tourne en
// loopback sur la même machine ; un client vu « from 192.168.x.x » côté Login a besoin de l'IP LAN du serveur.
// Override explicite via la variable d'env FENRIR_PUBLIC_HOST, sinon auto-détection de l'IPv4 LAN primaire.
var gamePublicHost = Environment.GetEnvironmentVariable("FENRIR_PUBLIC_HOST") is { Length: > 0 } configuredHost
    ? configuredHost
    : ResolvePrimaryLanIPv4();
Console.WriteLine(
    $"[Fenrir.AppHost] Game__PublicHost = {gamePublicHost} (override with the FENRIR_PUBLIC_HOST env var). " +
    "Clients receive this host to reach zones; it MUST be reachable from the client machine.");

// Base d'adressage legacy des zones (1100 + numéroDeMap). Le GameServer binde un listener par map hébergée sur
// ZoneBasePort + mapId ; le LoginServer dérive le MÊME port pour router le client (modèle « zone = endpoint »,
// Décision A / doc 03_Topologie_TCP_et_Aspire.md). Injecté aux deux depuis cette source unique pour éviter tout
// désaccord Login/Game.
const int zoneBasePort = 1100;

var sql = builder.AddSqlServer("sqlserver", sqlPassword)
    .WithImageTag("2025-latest")
    .WithDataVolume("fenrir-sql-data")
    .WithLifetime(ContainerLifetime.Persistent);

var fenrirDb = sql.AddDatabase("FenrirDb");

RemoveDefaultHealthCheck(sql.Resource);
RemoveDefaultHealthCheck(fenrirDb.Resource);

var migrator = builder.AddProject<Fenrir_Tools_DbMigrator>("db-migrator")
    .WithReference(fenrirDb)
    .WaitForStart(fenrirDb);

const int centerPort = 12003;
var center = builder.AddProject<Fenrir_CenterServer>("center-server")
    .WithReference(fenrirDb)
    .WaitForCompletion(migrator)
    .WithEndpoint(name: "center-tcp", scheme: "tcp", port: centerPort, targetPort: centerPort, isProxied: false)
    .WithEnvironment("Center__Port", centerPort.ToString())
    .WithEnvironment("Center__SharedSecret", centerSharedSecret);

var centerEndpoint = center.GetEndpoint("center-tcp");

const int loginPort = 29998;
builder.AddProject<Fenrir_LoginServer>("login-server")
    .WithReference(fenrirDb)
    .WithReference(centerEndpoint)
    .WaitForCompletion(migrator)
    .WithEndpoint(name: "login-tcp", scheme: "tcp", port: loginPort, targetPort: loginPort, isProxied: false)
    .WithEnvironment("Login__Port", loginPort.ToString())
    .WithEnvironment("Login__ZoneBasePort", zoneBasePort.ToString())
    .WithEnvironment("Center__Endpoint", centerEndpoint)
    .WithEnvironment("Center__SharedSecret", centerSharedSecret);

byte[] shardIds = [1];

foreach (var shardId in shardIds)
{
    // Endpoint « ancre » pour le dashboard : le port de la 1re map du shard (1100 + shardId). Le GameServer ouvre
    // en réalité UN listener par map hébergée (ZoneBasePort + mapId) -- isProxied:false, donc le process binde
    // lui-même tous ces ports et le client les atteint en direct (Aspire n'est pas sur le chemin des paquets).
    var anchorZonePort = zoneBasePort + shardId;

    builder.AddProject<Fenrir_GameServer>($"game-shard-{shardId:00}")
        .WithReference(fenrirDb)
        .WithReference(centerEndpoint)
        .WaitForCompletion(migrator)
        .WithEndpoint(name: "zone-tcp", scheme: "tcp", port: anchorZonePort, targetPort: anchorZonePort,
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

// IPv4 LAN primaire = l'adresse locale de la NIC qui sortirait vers l'extérieur. Le « Connect » UDP n'émet
// AUCUN paquet (datagramme sans handshake) : il ne fait que résoudre l'interface de sortie, donc pas besoin que
// 8.8.8.8 soit joignable. Repli sur 127.0.0.1 si l'hôte n'a aucune route (machine isolée).
static string ResolvePrimaryLanIPv4()
{
    try
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Connect("8.8.8.8", 65530);
        return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
    }
    catch (SocketException)
    {
        return "127.0.0.1";
    }
}
