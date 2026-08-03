using Microsoft.Data.SqlClient;

namespace Fenrir.Tools.DbMigrator;

public static class DatabaseProvisioner
{
    public static async Task<bool> EnsureCreatedAsync(string connectionString, bool recreate = false)
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
        var quotedLiteral = databaseName.Replace("'", "''");

        if (recreate)
        {
            await using var dropDbCommand = new SqlCommand(
                $"IF DB_ID(N'{quotedLiteral}') IS NOT NULL " +
                $"BEGIN ALTER DATABASE [{quotedName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{quotedName}]; END;",
                masterConnection);
            await dropDbCommand.ExecuteNonQueryAsync();
            Console.WriteLine($"Database '{databaseName}' dropped (--recreate).");
        }

        await using var createDbCommand = new SqlCommand(
            $"IF DB_ID(N'{quotedLiteral}') IS NULL CREATE DATABASE [{quotedName}];",
            masterConnection);
        await createDbCommand.ExecuteNonQueryAsync();

        Console.WriteLine($"Database '{databaseName}' ready.");
        return true;
    }
}
