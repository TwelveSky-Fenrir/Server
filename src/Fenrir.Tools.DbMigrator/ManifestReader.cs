namespace Fenrir.Tools.DbMigrator;

public static class ManifestReader
{
    public static async Task<IReadOnlyList<string>> ReadScriptPathsAsync(string manifestPath)
    {
        var lines = await File.ReadAllLinesAsync(manifestPath);

        return lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();
    }
}
