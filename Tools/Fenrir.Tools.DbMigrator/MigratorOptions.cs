namespace Fenrir.Tools.DbMigrator;

public sealed record MigratorOptions(string ConnectionString, string DatabaseDirectory, string EnvironmentName)
{
    public const string DevOnlySeedScriptPath = "Migrations/Seed/001_dev_account.sql";

    public bool IsDevelopmentEnvironment =>
        string.Equals(EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);

    public string ManifestPath => Path.Combine(DatabaseDirectory, "_manifest.txt");

    public static MigratorOptions? FromEnvironment(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__FenrirDb") ??
            Environment.GetEnvironmentVariable("FENRIR_DB_CONNECTION_STRING") ??
            args.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        var environmentName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
            "Production";

        var databaseDirectory = Path.Combine(AppContext.BaseDirectory, "Database");

        return new MigratorOptions(connectionString, databaseDirectory, environmentName);
    }
}
