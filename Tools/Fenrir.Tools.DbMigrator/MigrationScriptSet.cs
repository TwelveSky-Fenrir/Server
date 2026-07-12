using System.Security.Cryptography;
using System.Text;

namespace Fenrir.Tools.DbMigrator;

public sealed record LoadedScript(string RelativePath, string Content, byte[] Hash);

public static class MigrationScriptSet
{
    /// <summary>
    /// Reads and hashes every script the manifest references, fully, before anything touches the database.
    /// A missing file fails the whole run here -- never partway through applying earlier scripts.
    /// </summary>
    public static async Task<IReadOnlyList<LoadedScript>?> LoadAsync(string databaseDirectory,
        IReadOnlyList<string> relativePaths)
    {
        var loaded = new List<LoadedScript>(relativePaths.Count);

        foreach (var relativePath in relativePaths)
        {
            var scriptPath = Path.Combine(databaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(scriptPath))
            {
                Console.Error.WriteLine($"Manifest references a missing script: {relativePath}");
                return null;
            }

            var content = await File.ReadAllTextAsync(scriptPath);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));

            loaded.Add(new LoadedScript(relativePath, content, hash));
        }

        return loaded;
    }
}
