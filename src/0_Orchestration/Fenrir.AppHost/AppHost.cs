using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", true);

var centerSharedSecret = builder.AddParameter("center-shared-secret", true);

const string gamePublicHost = "127.0.0.1";

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
    .WithEnvironment("Center__Endpoint", centerEndpoint)
    .WithEnvironment("Center__SharedSecret", centerSharedSecret);

const int zoneBasePort = 1100;
byte[] shardIds = [1];

foreach (var shardId in shardIds)
{
    var zonePort = zoneBasePort + shardId;

    builder.AddProject<Fenrir_GameServer>($"game-shard-{shardId:00}")
        .WithReference(fenrirDb)
        .WithReference(centerEndpoint)
        .WaitForCompletion(migrator)
        .WithEndpoint(name: "zone-tcp", scheme: "tcp", port: zonePort, targetPort: zonePort, isProxied: false)
        .WithEnvironment("Game__ShardId", shardId.ToString())
        .WithEnvironment("Game__Port", zonePort.ToString())
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
