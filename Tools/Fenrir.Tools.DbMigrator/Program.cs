using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

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

// Fail-closed dev-only-fixture gate (seed-data-review finding: 001_dev_account.sql had zero environment
// gating anywhere in the pipeline). Mirrors ASP.NET Core's own DOTNET_ENVIRONMENT/ASPNETCORE_ENVIRONMENT
// precedence and "Production" default (Microsoft Learn, "ASP.NET Core runtime environments"), so an
// unset environment -- the only thing a real non-dev deployment is guaranteed not to override to
// "Development" -- skips the seed rather than applying it. Local Aspire runs keep working because
// Orchestration/Fenrir.AppHost/Properties/launchSettings.json's own profiles set DOTNET_ENVIRONMENT=
// Development, which child project resources inherit via standard OS process-environment inheritance.
// If Aspire's project-resource process spawning is ever confirmed to NOT inherit the AppHost's ambient
// environment, AppHost.cs needs an explicit .WithEnvironment("DOTNET_ENVIRONMENT", ...) on db-migrator --
// that wiring is fenrir-aspire-hosting-engineer's call, not this tool's.
var environmentName =
    Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
    "Production";
var isDevelopmentEnvironment = string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
const string devOnlySeedScriptPath = "Migrations/Seed/001_dev_account.sql";

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

const int maxAttempts = 10;

var targetBuilder = new SqlConnectionStringBuilder(connectionString);
var databaseName = targetBuilder.InitialCatalog;

if (!string.IsNullOrEmpty(databaseName))
{
    var masterBuilder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
    await using var masterConnection = new SqlConnection(masterBuilder.ConnectionString);

    for (var attempt = 1;; attempt++)
        try
        {
            await masterConnection.OpenAsync();
            break;
        }
        catch (SqlException ex) when (attempt < maxAttempts)
        {
            Console.WriteLine($"Connection attempt {attempt}/{maxAttempts} failed: {ex.Message}. Retrying in 3s...");
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
        catch (SqlException ex)
        {
            Console.Error.WriteLine($"Could not connect after {maxAttempts} attempts: {ex.Message}");
            return 1;
        }

    var quotedName = databaseName.Replace("]", "]]");
    await using var createDbCommand = new SqlCommand(
        $"IF DB_ID(N'{databaseName.Replace("'", "''")}') IS NULL CREATE DATABASE [{quotedName}];", masterConnection);
    await createDbCommand.ExecuteNonQueryAsync();
    Console.WriteLine($"Database '{databaseName}' ready.");
}

await using var connection = new SqlConnection(connectionString);

for (var attempt = 1;; attempt++)
    try
    {
        await connection.OpenAsync();
        break;
    }
    catch (SqlException ex) when (attempt < maxAttempts)
    {
        Console.WriteLine($"Connection attempt {attempt}/{maxAttempts} failed: {ex.Message}. Retrying in 3s...");
        await Task.Delay(TimeSpan.FromSeconds(3));
    }
    catch (SqlException ex)
    {
        Console.Error.WriteLine($"Could not connect after {maxAttempts} attempts: {ex.Message}");
        return 1;
    }

var journalReady = await JournalTableExistsAsync(connection);
var applied = journalReady
    ? await LoadAppliedScriptsAsync(connection)
    : new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

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

    if (relativePath.Equals(devOnlySeedScriptPath, StringComparison.OrdinalIgnoreCase) && !isDevelopmentEnvironment)
    {
        Console.WriteLine($"skip   {relativePath} (dev-only fixture; environment '{environmentName}' is not Development)");
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

await ReportIndexedViewArithabortStatusAsync(connection);

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

static async Task ReportIndexedViewArithabortStatusAsync(SqlConnection connection)
{
    // world.vw_ItemMallCatalog / game.vw_OfflineShopListing are WITH SCHEMABINDING indexed views. The query
    // optimizer only substitutes an indexed view's materialized index for a session with ARITHABORT
    // effectively ON (Microsoft Learn, "Create indexed views" -- required SET options table).
    // Microsoft.Data.SqlClient/CaeriusNet negotiates OLE DB/ODBC-style session defaults, which set
    // ANSI_WARNINGS ON but leave the explicit ARITHABORT bit OFF. Per Microsoft Learn's "SET ARITHABORT
    // (Transact-SQL)" Remarks, though: "When ANSI_WARNINGS has a value of ON and the database compatibility
    // level is set to 90 or higher then ARITHABORT is implicitly ON regardless of its value setting." Fenrir
    // never sets an explicit COMPATIBILITY_LEVEL override anywhere under Database/, so this database inherits
    // the SQL Server 2025 instance's own native compat level (well above the 90 floor) -- meaning every
    // CaeriusNet connection should already satisfy the requirement without any server-wide sp_configure
    // change. This check reports the actual, observed values from the exact driver/connection type CaeriusNet
    // itself uses, on every real db-migrator run, instead of leaving that "should" as a one-off manual step
    // nobody ever gets around to running against a live instance. A wrong ARITHABORT setting on a READING
    // connection only degrades performance (the optimizer silently falls back to the base tables) -- but per
    // Microsoft Learn ("SET ARITHABORT (Transact-SQL)"), ARITHABORT OFF on a WRITING connection makes any
    // INSERT/UPDATE/DELETE against game.OfflineShops/game.OfflineShopItems (actively written by live gameplay
    // procedures) fail outright with an error, not merely degrade. This check itself only ever reports a
    // diagnostic, never fails the migration, since it cannot distinguish read-only from write-capable
    // connections from here.
    try
    {
        await using var command = new SqlCommand(
            "SELECT CASE WHEN (64 & @@OPTIONS) = 64 THEN 1 ELSE 0 END, " +
            "(SELECT compatibility_level FROM sys.databases WHERE database_id = DB_ID());", connection);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return;

        var arithabortOn = reader.GetInt32(0) == 1;
        var compatibilityLevel = reader.GetByte(1);

        Console.WriteLine(arithabortOn
            ? $"ARITHABORT effectively ON (compatibility level {compatibilityLevel}) -- world.vw_ItemMallCatalog / game.vw_OfflineShopListing's indexes are eligible for optimizer use on this connection type."
            : $"WARNING: ARITHABORT effectively OFF (compatibility level {compatibilityLevel}). For read-only queries this only degrades performance (the optimizer falls back to querying world.vw_ItemMallCatalog / game.vw_OfflineShopListing's base tables directly). For writes against game.OfflineShops/game.OfflineShopItems (actively written by live gameplay procedures), this setting makes the write fail outright with an error instead. Expected ON via the OLE DB/ODBC ANSI_WARNINGS default plus compatibility level >= 90; check whether COMPATIBILITY_LEVEL was explicitly lowered below 90.");
    }
    catch (SqlException ex)
    {
        Console.WriteLine($"Could not verify ARITHABORT/compatibility level (non-fatal, diagnostic only): {ex.Message}");
    }
}

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
