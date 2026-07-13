using Projects;

// =====================================================================================================
// Topologie Aspire (Lot F2) — PLAN DE CONTRÔLE UNIQUEMENT (ADR-0003). Aucune trame de jeu ne traverse
// Aspire : il découvre et injecte les endpoints, il ne relaie rien. TOUT-TCP, ports fidèles au legacy.
// Doc : Roadmap/FONDATIONS/03_Topologie_TCP_et_Aspire.md.
//
//                    sqlserver ──WaitFor──▶ db-migrator ──WaitForCompletion──▶ Center / Login / Zones
//
// Fidélité legacy : Login PUBLIC 29998 · Zone PUBLIC 1100+N · Center INTERNE 12003.
// =====================================================================================================
var builder = DistributedApplication.CreateBuilder(args);

// ── SQL Server 2025 : conteneur persistant, volume de données (survit aux redémarrages) ──────────────
var sqlPassword = builder.AddParameter("sql-password", true);

var sql = builder.AddSqlServer("sqlserver", sqlPassword)
    .WithImageTag("2025-latest")
    .WithDataVolume("fenrir-sql-data")
    .WithLifetime(ContainerLifetime.Persistent);

var fenrirDb = sql.AddDatabase("FenrirDb");

// On retire le health-check par défaut que `AddSqlServer` attache aux ressources SQL (sonde de connexion) :
// l'ordonnancement de readiness est porté explicitement par `WaitForStart(fenrirDb)` (migrator) puis
// `WaitForCompletion(migrator)` (serveurs), et la robustesse de connexion par le retry SqlClient/CaeriusNet.
// Laisser la sonde par défaut la ferait apparaître "unhealthy" pendant le warm-up du conteneur sans valeur ajoutée.
RemoveDefaultHealthCheck(sql.Resource);
RemoveDefaultHealthCheck(fenrirDb.Resource);

// ── Migrateur Data-First : applique _manifest.txt puis SE TERMINE (jamais résident) ──────────────────
var migrator = builder.AddProject<Fenrir_Tools_DbMigrator>("db-migrator")
    .WithReference(fenrirDb)
    .WaitForStart(fenrirDb);

// ── CenterServer : INTERNE (jamais client-facing), coordination cross-zone (fidèle à ts25center) ─────
const int centerPort = 12003;
var center = builder.AddProject<Fenrir_CenterServer>("center-server")
    .WithReference(fenrirDb)
    .WaitForCompletion(migrator)
    .WithEndpoint(name: "center-tcp", scheme: "tcp", port: centerPort, targetPort: centerPort, isProxied: false)
    .WithEnvironment("Center__Port", centerPort.ToString());

// ── LoginServer : PUBLIC. Découvre le CenterServer par endpoint (TCP direct, jamais en dur / SHM) ────
const int loginPort = 29998;
builder.AddProject<Fenrir_LoginServer>("login-server")
    .WithReference(fenrirDb)
    .WithReference(center.GetEndpoint("center-tcp"))
    .WaitForCompletion(migrator)
    .WithEndpoint(name: "login-tcp", scheme: "tcp", port: loginPort, targetPort: loginPort, isProxied: false)
    .WithEnvironment("Login__Port", loginPort.ToString());

// ── GameServers : PUBLIC. Un endpoint zone-tcp par shard, port 1100 + N (fidèle à ts25zone). ─────────
//    Un process peut exposer 1..N listeners de zone (densité de déploiement) — détail F3, doc 04.
const int zoneBasePort = 1100;
byte[] shardIds = [1];

foreach (var shardId in shardIds)
{
    var zonePort = zoneBasePort + shardId; // shard/zone 1 → 1101 (legacy : zone N → 1100 + N)

    builder.AddProject<Fenrir_GameServer>($"game-shard-{shardId:00}")
        .WithReference(fenrirDb)
        .WithReference(center.GetEndpoint("center-tcp"))
        .WaitForCompletion(migrator)
        .WithEndpoint(name: "zone-tcp", scheme: "tcp", port: zonePort, targetPort: zonePort, isProxied: false)
        .WithEnvironment("Game__ShardId", shardId.ToString())
        .WithEnvironment("Game__Port", zonePort.ToString());
}

builder.Build().Run();
return;

// Retire les HealthCheckAnnotation par défaut d'une ressource SQL (readiness portée par WaitFor* + retry SqlClient).
static void RemoveDefaultHealthCheck(IResource resource)
{
    foreach (var annotation in resource.Annotations.OfType<HealthCheckAnnotation>().ToArray())
        resource.Annotations.Remove(annotation);
}
