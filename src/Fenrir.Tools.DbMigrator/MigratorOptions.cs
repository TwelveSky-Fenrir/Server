namespace Fenrir.Tools.DbMigrator;

public sealed record MigratorOptions(string ConnectionString, string DatabaseDirectory, string EnvironmentName,
    bool Recreate = false)
{
    public const string DevOnlySeedScriptPath = "Migrations/Seed/001_dev_account.sql";

    public bool IsDevelopmentEnvironment =>
        string.Equals(EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);

    public string ManifestPath => Path.Combine(DatabaseDirectory, "_manifest.txt");

    public static MigratorOptions? FromEnvironment(string[] args)
    {
        var recreate = args.Contains("--recreate", StringComparer.OrdinalIgnoreCase);
        var positionalArgs = args.Where(a => !a.Equals("--recreate", StringComparison.OrdinalIgnoreCase)).ToArray();

        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__FenrirDb") ??
            Environment.GetEnvironmentVariable("FENRIR_DB_CONNECTION_STRING") ??
            positionalArgs.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        var environmentName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
            "Production";

        var databaseDirectory = Path.Combine(AppContext.BaseDirectory, "Database");

        return new MigratorOptions(connectionString, databaseDirectory, environmentName, recreate);
    }
}
