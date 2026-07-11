using System.Text;
using Aspire.Hosting.Testing;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Tests.Fixtures;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private static readonly string[] NoArgs = [];

    private DistributedApplication? _app;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var builder = DistributedApplicationTestingBuilder.Create(NoArgs);

        builder.AddSqlServer("sqlserver")
            .WithImageTag("2025-latest")
            .AddDatabase("FenrirDbTest");

        _app = await builder.BuildAsync();
        await _app.StartAsync();

        using var readyCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("FenrirDbTest", readyCts.Token);

        ConnectionString = await _app.GetConnectionStringAsync("FenrirDbTest") ??
                           throw new InvalidOperationException(
                               "The \"FenrirDbTest\" resource did not produce a connection string even though " +
                               "it just reported healthy.");

        await ApplyManifestAsync();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }

    private async Task ApplyManifestAsync()
    {
        var databaseDir = Path.Combine(AppContext.BaseDirectory, "Database");
        var manifestPath = Path.Combine(databaseDir, "_manifest.txt");

        var scriptPaths = (await File.ReadAllLinesAsync(manifestPath))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        foreach (var relativePath in scriptPaths)
        {
            var scriptPath = Path.Combine(databaseDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var content = await File.ReadAllTextAsync(scriptPath);

            foreach (var batch in SplitBatches(content))
            {
                if (batch.Length == 0)
                    continue;

                await using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    private static IEnumerable<string> SplitBatches(string script)
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
}
