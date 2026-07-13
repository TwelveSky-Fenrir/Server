using System.Data;
using Microsoft.Data.SqlClient;

namespace Fenrir.Tools.DbMigrator;

public static class SchemaVersionJournal
{
    public static async Task<bool> TableExistsAsync(SqlConnection connection)
    {
        await using var command =
            new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'admin.SchemaVersions') IS NOT NULL THEN 1 ELSE 0 END;",
                connection);
        return (int)(await command.ExecuteScalarAsync())! == 1;
    }

    public static async Task<Dictionary<string, byte[]>> LoadAppliedScriptsAsync(SqlConnection connection)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        await using var command = new SqlCommand("SELECT ScriptName, Sha256 FROM admin.SchemaVersions;", connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            result[reader.GetString(0)] = (byte[])reader[1];

        return result;
    }

    public static async Task RecordAsync(SqlConnection connection, string scriptName, byte[] hash)
    {
        await using var command = new SqlCommand(
            "INSERT INTO admin.SchemaVersions (ScriptName, Sha256, AppliedAtUtc) VALUES (@ScriptName, @Sha256, SYSUTCDATETIME());",
            connection);
        command.Parameters.AddWithValue("@ScriptName", scriptName);
        command.Parameters.Add("@Sha256", SqlDbType.Binary, 32).Value = hash;
        await command.ExecuteNonQueryAsync();
    }
}
