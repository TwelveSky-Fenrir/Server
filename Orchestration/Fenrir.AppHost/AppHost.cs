using Aspire.Hosting.ApplicationModel;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// Persistent volume + container lifetime so the dev loop survives an `aspire run` restart without re-seeding.
var sqlPassword = builder.AddParameter("sql-password", true);

var sql = builder.AddSqlServer("sqlserver", sqlPassword)
    .WithImageTag("2025-latest")
    .WithDataVolume("fenrir-sql-data")
    .WithLifetime(ContainerLifetime.Persistent);

var fenrirDb = sql.AddDatabase("FenrirDb");

// AddSqlServer()/AddDatabase() each attach their own built-in HealthCheckAnnotation ("sqlserver_check",
// "FenrirDb_check"), but that check's connection string never becomes available on this Aspire build --
// it registers but is never invoked (see the WaitForStart note below), so the dashboard shows both
// resources as permanently Unhealthy/Unstable even though the container is fully up and serving traffic.
// Nothing here ever WaitFor()s on either check, so dropping the annotations only removes the misleading
// badge; it changes no startup ordering.
RemoveBrokenHealthCheck(sql.Resource);
RemoveBrokenHealthCheck(fenrirDb.Resource);

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

// Shard 1 hosts map 1 (tribe 0 spawn); shard 2 hosts maps 6/11/140 (tribes 1/2/4th-faction spawns) --
// see admin.ShardMapAssignments (Database/Migrations/Seed/admin/005_shard_map_assignments.sql). A
// character spawning on a map with no running shard silently fails EnterWorldService's zones.TryGet
// check (Abort, no packet sent), so every assigned map must have its shard running here.
const int gameBasePort = 1100;
byte[] shardIds = [1, 2];

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
return;

static void RemoveBrokenHealthCheck(IResource resource)
{
    foreach (var annotation in resource.Annotations.OfType<HealthCheckAnnotation>().ToArray())
        resource.Annotations.Remove(annotation);
}
