using Microsoft.Data.SqlClient;

namespace Fenrir.Tools.DbMigrator;

public static class MigrationRunner
{
    public static async Task<int> RunAsync(MigratorOptions options)
    {
        if (!File.Exists(options.ManifestPath))
        {
            Console.Error.WriteLine($"Manifest not found: {options.ManifestPath}");
            return 1;
        }

        var scriptPaths = await ManifestReader.ReadScriptPathsAsync(options.ManifestPath);

        // Read and hash every manifest-referenced script up front, before opening a database connection or
        // provisioning anything -- a missing file fails fast here instead of surfacing after some scripts
        // have already been applied.
        var scripts = await MigrationScriptSet.LoadAsync(options.DatabaseDirectory, scriptPaths);
        if (scripts is null)
            return 1;

        if (!await DatabaseProvisioner.EnsureCreatedAsync(options.ConnectionString))
            return 1;

        await using var connection = new SqlConnection(options.ConnectionString);
        if (!await SqlConnectionOpener.TryOpenAsync(connection))
            return 1;

        var journalReady = await SchemaVersionJournal.TableExistsAsync(connection);
        var applied = journalReady
            ? await SchemaVersionJournal.LoadAppliedScriptsAsync(connection)
            : new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        var pendingJournal = new List<(string RelativePath, byte[] Hash)>();

        foreach (var script in scripts)
        {
            var outcome = await ApplyScriptAsync(connection, options, script, applied, journalReady, pendingJournal);

            if (outcome == ScriptOutcome.Failed)
                return 1;

            if (outcome == ScriptOutcome.Applied)
                journalReady = await FlushPendingJournalAsync(connection, journalReady, pendingJournal);
        }

        if (pendingJournal.Count > 0)
            Console.Error.WriteLine(
                "Warning: admin.SchemaVersions was never created by any script in the manifest -- nothing could be journaled, so every script above will be re-applied next run.");

        await IndexedViewArithabortDiagnostics.ReportAsync(connection);

        Console.WriteLine($"Migration complete: {scripts.Count} script(s) checked.");
        return 0;
    }

    private static async Task<ScriptOutcome> ApplyScriptAsync(SqlConnection connection, MigratorOptions options,
        LoadedScript script, Dictionary<string, byte[]> applied, bool journalReady,
        List<(string RelativePath, byte[] Hash)> pendingJournal)
    {
        var (relativePath, content, hash) = script;

        if (applied.TryGetValue(relativePath, out var appliedHash))
        {
            if (!appliedHash.AsSpan().SequenceEqual(hash))
            {
                Console.Error.WriteLine(
                    $"'{relativePath}' was already applied with a different hash. History must never be rewritten -- add a new corrective script instead of editing this one.");
                return ScriptOutcome.Failed;
            }

            Console.WriteLine($"skip   {relativePath} (already applied)");
            return ScriptOutcome.Skipped;
        }

        if (relativePath.Equals(MigratorOptions.DevOnlySeedScriptPath, StringComparison.OrdinalIgnoreCase) &&
            !options.IsDevelopmentEnvironment)
        {
            Console.WriteLine(
                $"skip   {relativePath} (dev-only fixture; environment '{options.EnvironmentName}' is not Development)");
            return ScriptOutcome.Skipped;
        }

        Console.WriteLine($"apply  {relativePath}");

        foreach (var batch in SqlScriptBatcher.SplitBatches(content))
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
                return ScriptOutcome.Failed;
            }
        }

        if (journalReady)
            await SchemaVersionJournal.RecordAsync(connection, relativePath, hash);
        else
            pendingJournal.Add((relativePath, hash));

        applied[relativePath] = hash;
        return ScriptOutcome.Applied;
    }

    private static async Task<bool> FlushPendingJournalAsync(SqlConnection connection, bool journalReady,
        List<(string RelativePath, byte[] Hash)> pendingJournal)
    {
        if (journalReady || pendingJournal.Count == 0)
            return journalReady;

        journalReady = await SchemaVersionJournal.TableExistsAsync(connection);

        if (!journalReady)
            return journalReady;

        foreach (var (pendingPath, pendingHash) in pendingJournal)
            await SchemaVersionJournal.RecordAsync(connection, pendingPath, pendingHash);

        pendingJournal.Clear();
        return journalReady;
    }

    private enum ScriptOutcome
    {
        Applied,
        Skipped,
        Failed
    }
}
