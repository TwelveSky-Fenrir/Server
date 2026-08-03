using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace Fenrir.Tools.DbMigrator;

public static class MigrationRunner
{
    public static async Task<int> RunAsync(MigratorOptions options)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!File.Exists(options.ManifestPath))
        {
            Console.Error.WriteLine($"Manifest not found: {options.ManifestPath}");
            return 1;
        }

        var scriptPaths = await ManifestReader.ReadScriptPathsAsync(options.ManifestPath);

        if (!ManifestIntegrityChecker.Validate(options.DatabaseDirectory, scriptPaths))
            return 1;

        var scripts = await MigrationScriptSet.LoadAsync(options.DatabaseDirectory, scriptPaths);
        if (scripts is null)
            return 1;

        if (!await DatabaseProvisioner.EnsureCreatedAsync(options.ConnectionString, options.Recreate))
            return 1;

        await using var connection = new SqlConnection(options.ConnectionString);
        if (!await SqlConnectionOpener.TryOpenAsync(connection))
            return 1;

        var journalReady = await SchemaVersionJournal.TableExistsAsync(connection);
        var applied = journalReady
            ? await SchemaVersionJournal.LoadAppliedScriptsAsync(connection)
            : new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        var pendingJournal = new List<(string RelativePath, byte[] Hash)>();
        var appliedCount = 0;
        var skippedCount = 0;

        foreach (var script in scripts)
        {
            var outcome = await ApplyScriptAsync(connection, options, script, applied, journalReady, pendingJournal);

            if (outcome == ScriptOutcome.Failed)
                return 1;

            if (outcome == ScriptOutcome.Applied)
            {
                appliedCount++;
                journalReady = await FlushPendingJournalAsync(connection, journalReady, pendingJournal);
            }
            else
            {
                skippedCount++;
            }
        }

        if (pendingJournal.Count > 0)
            Console.Error.WriteLine(
                "Warning: admin.SchemaVersions was never created by any script in the manifest -- nothing could be journaled, so every script above will be re-applied next run.");

        await IndexedViewSetOptionDiagnostics.ReportAsync(connection);

        Console.WriteLine(
            $"Migration complete: {scripts.Count} script(s) checked ({appliedCount} applied, {skippedCount} skipped) in {stopwatch.Elapsed.TotalSeconds:0.0}s.");
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
                    $"'{relativePath}' was already applied with a different hash. This database was migrated from an older version of that script, so its current state no longer matches the repository. Re-run with --recreate to drop and rebuild from scratch (the supported workflow for this project, which has no production database), or apply a corrective script if this instance must be preserved.");
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

        var batches = SqlScriptBatcher.SplitBatches(content).Where(b => b.Length > 0).ToList();

        for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
        {
            var batch = batches[batchIndex];

            try
            {
                await using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
                await command.ExecuteNonQueryAsync();
            }
            catch (SqlException ex)
            {
                ReportBatchFailure(relativePath, batchIndex + 1, batches.Count, batch, ex);
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

    private static void ReportBatchFailure(string relativePath, int batchIndex, int batchCount, string batch,
        SqlException ex)
    {
        Console.Error.WriteLine(
            $"'{relativePath}' failed on batch {batchIndex}/{batchCount} (error {ex.Number}" +
            (ex.LineNumber > 0 ? $", line {ex.LineNumber}" : "") +
            (string.IsNullOrEmpty(ex.Procedure) ? "" : $", in '{ex.Procedure}'") +
            $"): {ex.Message}");

        var preview = batch.Length > 300 ? batch[..300] + " [...]" : batch;
        Console.Error.WriteLine($"Failing batch text:\n{preview}");

        if (ex.Errors.Count > 1)
            foreach (SqlError error in ex.Errors)
                Console.Error.WriteLine($"  - [{error.Number}] {error.Message}");
    }

    private enum ScriptOutcome
    {
        Applied,
        Skipped,
        Failed
    }
}
