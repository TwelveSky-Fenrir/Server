namespace Fenrir.Tools.DbMigrator;

public static class ManifestIntegrityChecker
{
    public static bool Validate(string databaseDirectory, IReadOnlyList<string> listedScriptPaths)
    {
        var listed = new HashSet<string>(listedScriptPaths, StringComparer.OrdinalIgnoreCase);

        var duplicates = listedScriptPaths
            .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var onDisk = Directory
            .EnumerateFiles(databaseDirectory, "*.sql", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(databaseDirectory, path).Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unlisted = onDisk.Except(listed, StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (duplicates.Length == 0 && unlisted.Length == 0)
            return true;

        foreach (var duplicate in duplicates)
            Console.Error.WriteLine($"Manifest lists '{duplicate}' more than once.");

        foreach (var orphan in unlisted)
            Console.Error.WriteLine($"'{orphan}' exists on disk but is absent from the manifest.");

        Console.Error.WriteLine(
            "Manifest integrity check failed. Every .sql under Database/ must be listed exactly once, in dependency order.");
        return false;
    }
}
