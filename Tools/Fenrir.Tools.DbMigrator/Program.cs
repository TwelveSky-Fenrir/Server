using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

// Refuses to re-apply a journaled script whose hash changed -- history is never rewritten, only corrected forward.

var connectionString =
    Environment.GetEnvironmentVariable("ConnectionStrings__FenrirDb") ??
    Environment.GetEnvironmentVariable("FENRIR_DB_CONNECTION_STRING") ??
    args.FirstOrDefault();

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "No connection string. Set ConnectionStrings__FenrirDb (Aspire convention), FENRIR_DB_CONNECTION_STRING, or pass one as the first argument.");
    return 1;
}

var databaseDir = Path.Combine(AppContext.BaseDirectory, "Database");
var manifestPath = Path.Combine(databaseDir, "_manifest.txt");

if (!File.Exists(manifestPath))
{
    Console.Error.WriteLine($"Manifest not found: {manifestPath}");
    return 1;
}

var scriptPaths = (await File.ReadAllLinesAsync(manifestPath))
    .Select(line => line.Trim())
    .Where(line => line.Length > 0 && !line.StartsWith('#'))
    .ToArray();

await using var connection = new SqlConnection(connectionString);

try
{
    await connection.OpenAsync();
}
catch (SqlException ex)
{
    Console.Error.WriteLine($"Could not connect: {ex.Message}");
    return 1;
}

var journalReady = await JournalTableExistsAsync(connection);
var applied = journalReady
    ? await LoadAppliedScriptsAsync(connection)
    : new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

// admin.SchemaVersions can't journal scripts that run before it exists; buffer those and flush once it does.
var pendingJournal = new List<(string RelativePath, byte[] Hash)>();

foreach (var relativePath in scriptPaths)
{
    var scriptPath = Path.Combine(databaseDir, relativePath.Replace('/', Path.DirectorySeparatorChar));

    if (!File.Exists(scriptPath))
    {
        Console.Error.WriteLine($"Manifest references a missing script: {relativePath}");
        return 1;
    }

    var content = await File.ReadAllTextAsync(scriptPath);
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));

    if (applied.TryGetValue(relativePath, out var appliedHash))
    {
        if (!appliedHash.AsSpan().SequenceEqual(hash))
        {
            Console.Error.WriteLine(
                $"'{relativePath}' was already applied with a different hash. History must never be rewritten -- add a new corrective script instead of editing this one.");
            return 1;
        }

        Console.WriteLine($"skip   {relativePath} (already applied)");
        continue;
    }

    Console.WriteLine($"apply  {relativePath}");

    foreach (var batch in SplitBatches(content))
    {
        if (batch.Length == 0)
            continue;

        try
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
            await command.ExecuteNonQueryAsync();
        }
        catch (SqlException ex)
        {
            Console.Error.WriteLine($"'{relativePath}' failed: {ex.Message}");
            return 1;
        }
    }

    if (journalReady)
    {
        await JournalAsync(connection, relativePath, hash);
    }
    else
    {
        pendingJournal.Add((relativePath, hash));
        journalReady = await JournalTableExistsAsync(connection);

        if (journalReady)
        {
            foreach (var (pendingPath, pendingHash) in pendingJournal)
                await JournalAsync(connection, pendingPath, pendingHash);
            pendingJournal.Clear();
        }
    }

    applied[relativePath] = hash;
}

if (pendingJournal.Count > 0)
    Console.Error.WriteLine(
        "Warning: admin.SchemaVersions was never created by any script in the manifest -- nothing could be journaled, so every script above will be re-applied next run.");

Console.WriteLine($"Migration complete: {scriptPaths.Length} script(s) checked.");
return 0;

static async Task<bool> JournalTableExistsAsync(SqlConnection connection)
{
    await using var command =
        new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'admin.SchemaVersions') IS NOT NULL THEN 1 ELSE 0 END;",
            connection);
    return (int)(await command.ExecuteScalarAsync())! == 1;
}

static async Task<Dictionary<string, byte[]>> LoadAppliedScriptsAsync(SqlConnection connection)
{
    var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

    await using var command = new SqlCommand("SELECT ScriptName, Sha256 FROM admin.SchemaVersions;", connection);
    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
        result[reader.GetString(0)] = (byte[])reader[1];

    return result;
}

static async Task JournalAsync(SqlConnection connection, string scriptName, byte[] hash)
{
    await using var command = new SqlCommand(
        "INSERT INTO admin.SchemaVersions (ScriptName, Sha256, AppliedAtUtc) VALUES (@ScriptName, @Sha256, SYSUTCDATETIME());",
        connection);
    command.Parameters.AddWithValue("@ScriptName", scriptName);
    command.Parameters.Add("@Sha256", SqlDbType.Binary, 32).Value = hash;
    await command.ExecuteNonQueryAsync();
}

// SQL Server requires some statements (CREATE SCHEMA, CREATE/ALTER PROCEDURE...) to be alone in their batch.
static IEnumerable<string> SplitBatches(string script)
{
    var batch = new StringBuilder();

    foreach (var line in script.Replace("\r\n", "\n").Split('\n'))
        if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
        {
            yield return batch.ToString().Trim();
            batch.Clear();
        }
        else
        {
            batch.AppendLine(line);
        }

    if (batch.Length > 0)
        yield return batch.ToString().Trim();
}
