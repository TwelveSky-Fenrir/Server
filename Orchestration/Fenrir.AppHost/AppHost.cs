using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", true);

var sql = builder.AddSqlServer("sqlserver", sqlPassword)
    .WithImageTag("2025-latest")
    .WithDataVolume("fenrir-sql-data")
    .WithLifetime(ContainerLifetime.Persistent);

var fenrirDb = sql.AddDatabase("FenrirDb");

RemoveBrokenHealthCheck(sql.Resource);
RemoveBrokenHealthCheck(fenrirDb.Resource);

var migrator = builder.AddProject<Fenrir_Tools_DbMigrator>("db-migrator")
    .WithReference(fenrirDb)
    .WaitForStart(fenrirDb);

const int loginPort = 29998;
builder.AddProject<Fenrir_LoginServer>("login-server")
    .WithReference(fenrirDb)
    .WaitForCompletion(migrator)
    .WithEndpoint(name: "login-tcp", scheme: "tcp", port: loginPort, targetPort: loginPort, isProxied: false)
    .WithEnvironment("Login__Port", loginPort.ToString());

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
}

builder.Build().Run();
return;

static void RemoveBrokenHealthCheck(IResource resource)
{
    foreach (var annotation in resource.Annotations.OfType<HealthCheckAnnotation>().ToArray())
        resource.Annotations.Remove(annotation);
}
