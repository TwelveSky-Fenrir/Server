using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// SQL Server 2025 -- persistent volume + persistent container lifetime so the dev loop survives an
// `aspire run` restart without re-seeding every time (architecture reference §2.2).
var sqlPassword = builder.AddParameter("sql-password", true);

var sql = builder.AddSqlServer("sqlserver", sqlPassword)
    .WithImageTag("2025-latest")
    .WithDataVolume("fenrir-sql-data")
    .WithLifetime(ContainerLifetime.Persistent);

var fenrirDb = sql.AddDatabase("FenrirDb");

// Data-first migrator: applies Database/_manifest.txt then exits. Every server WaitForCompletion(migrator)
// below -- never a server started against a stale/missing schema (Phase 4).
var migrator = builder.AddProject<Fenrir_Tools_DbMigrator>("db-migrator")
    .WithReference(fenrirDb)
    .WaitFor(fenrirDb);

// LoginServer -- TCP-only socket server (Phase 5), not an HTTP service: no health-check endpoint is wired
// here on purpose (see Docs/adr/ADR-0008-no-http-on-game-servers.md). Aspire still tracks process liveness
// for the dashboard without one. Port matches the legacy client's ServerInfo.ini [Login.Server] and
// LoginServerOptions.Port's own default -- kept explicit here so AppHost's declared topology and the app's
// actual bound port can never silently drift apart.
const int loginPort = 29998;

builder.AddProject<Fenrir_LoginServer>("login-server")
    .WithReference(fenrirDb)
    .WaitForCompletion(migrator)
    .WithEndpoint(name: "login-tcp", scheme: "tcp", port: loginPort, targetPort: loginPort, isProxied: false)
    .WithEnvironment("Login__Port", loginPort.ToString());

// GameServer -- M1 ships exactly one shard/one spawn map (architecture reference: "1 seule map de spawn
// suffit"). Still expressed as a loop over shard ids (Decision D-01: "un shard = une ressource, pas des
// replicas") so a future milestone adding a second shard is a one-line array change, not a restructuring.
const int gameBasePort = 1100;
byte[] shardIds = [1];

foreach (var shardId in shardIds)
{
    var gamePort = gameBasePort + shardId - 1;

    builder.AddProject<Fenrir_GameServer>($"game-shard-{shardId:00}")
        .WithReference(fenrirDb)
        .WaitForCompletion(migrator)
        .WithEndpoint(name: "game-tcp", scheme: "tcp", port: gamePort, targetPort: gamePort, isProxied: false)
        .WithEnvironment("Game__ShardId", shardId.ToString())
        .WithEnvironment("Game__Port", gamePort.ToString())
        // One zone actor per entry; a second map is Game__Maps__1, etc. Across shards the sets MUST stay
        // disjoint (ADR-0012) -- these keys override appsettings.json's Game:Maps index-by-index.
        .WithEnvironment("Game__Maps__0", "1");
}

builder.Build().Run();
