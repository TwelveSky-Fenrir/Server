using Microsoft.Data.SqlClient;

namespace Fenrir.Tools.DbMigrator;

public static class DatabaseProvisioner
{
    public static async Task<bool> EnsureCreatedAsync(string connectionString)
    {
        var targetBuilder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = targetBuilder.InitialCatalog;

        if (string.IsNullOrEmpty(databaseName))
            return true;

        var masterBuilder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
        await using var masterConnection = new SqlConnection(masterBuilder.ConnectionString);

        if (!await SqlConnectionOpener.TryOpenAsync(masterConnection))
            return false;

        var quotedName = databaseName.Replace("]", "]]");
        await using var createDbCommand = new SqlCommand(
            $"IF DB_ID(N'{databaseName.Replace("'", "''")}') IS NULL CREATE DATABASE [{quotedName}];",
            masterConnection);
        await createDbCommand.ExecuteNonQueryAsync();

        Console.WriteLine($"Database '{databaseName}' ready.");
        return true;
    }
}
