using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// Persistent volume + container lifetime so the dev loop survives an `aspire run` restart without re-seeding.
var sqlPassword = builder.AddParameter("sql-password", true);

var sql = builder.AddSqlServer("sqlserver", sqlPassword)
    .WithImageTag("2025-latest")
    .WithDataVolume("fenrir-sql-data")
    .WithLifetime(ContainerLifetime.Persistent);

var fenrirDb = sql.AddDatabase("FenrirDb");

// Applies Database/_manifest.txt then exits; every server WaitForCompletion(migrator) below.
// WaitForStart (not WaitFor) -- the built-in sqlserver/FenrirDb health check never reports a
// result on this Aspire build (registers but is never invoked), which would otherwise hang
// the whole app forever. DbMigrator retries its own connection instead.
var migrator = builder.AddProject<Fenrir_Tools_DbMigrator>("db-migrator")
    .WithReference(fenrirDb)
    .WaitForStart(fenrirDb);

const int loginPort = 29998;
builder.AddProject<Fenrir_LoginServer>("login-server")
    .WithReference(fenrirDb)
    .WaitForCompletion(migrator)
    .WithEndpoint(name: "login-tcp", scheme: "tcp", port: loginPort, targetPort: loginPort, isProxied: false)
    .WithEnvironment("Login__Port", loginPort.ToString());

// M1 ships exactly one shard; still a loop over shard ids so adding a second is a one-line array change.
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
        .WithEnvironment("Game__Port", gamePort.ToString());
    // Hosted maps come from admin.ShardMapAssignments, not config; assigned sets must stay disjoint across shards.
}

builder.Build().Run();
