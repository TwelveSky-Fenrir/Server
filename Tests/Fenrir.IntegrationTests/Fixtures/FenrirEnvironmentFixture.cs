using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Aspire.Hosting.Testing;
using Fenrir.Application.Game.Hosting;
using Fenrir.Application.Login.Hosting;
using Fenrir.Data.Security;
using Microsoft.Data.SqlClient;

namespace Fenrir.IntegrationTests.Fixtures;

/// <summary>
///     Boots one shared SQL Server 2025 container (SqlServerFixture-style: bare
///     <see cref="DistributedApplicationTestingBuilder" />, manifest applied by hand, no real AppHost involved),
///     then launches the ACTUAL Fenrir.LoginServer and Fenrir.GameServer executables as plain child processes
///     against it -- not orchestrated via Aspire's <c>AddProject</c>, so this fixture (not a fixed AppHost
///     dependency graph) fully controls seeding order: every extra row below lands before either server process
///     ever reads the database.
/// </summary>
public sealed class FenrirEnvironmentFixture : IAsyncLifetime
{
    private static readonly string[] NoArgs = [];

    public const byte ShardId = 1;
    public const short PrimaryMapId = 1;
    public const short SecondaryMapId = 2;

    public const string TestAccountLoginName = "e2ebot";
    public const string TestAccountPassword = "E2E-bot-p4ssw0rd!";

    private DistributedApplication? _app;
    private Process? _loginProcess;
    private Process? _gameProcess;
    private readonly StringBuilder _loginLog = new();
    private readonly StringBuilder _gameLog = new();
    private readonly Lock _loginLogLock = new();
    private readonly Lock _gameLogLock = new();

    public string ConnectionString { get; private set; } = string.Empty;
    public int LoginPort { get; private set; }
    public int GamePort { get; private set; }

    public async Task InitializeAsync()
    {
        var accountId = await StartDatabaseWithRetryAsync();

        LoginPort = ReserveEphemeralLoopbackPort();
        GamePort = ReserveEphemeralLoopbackPort();

        _loginProcess = StartServerProcess(
            OriginalBuildOutputDllPath(typeof(LoginConnectionHost).Assembly),
            _loginLog, _loginLogLock,
            new Dictionary<string, string?>
            {
                ["ConnectionStrings__FenrirDb"] = ConnectionString,
                ["Login__Port"] = LoginPort.ToString()
            });
        await WaitForServerReadyAsync(_loginProcess, LoginPort, "LoginServer", _loginLog, _loginLogLock);

        _gameProcess = StartServerProcess(
            OriginalBuildOutputDllPath(typeof(GameConnectionHost).Assembly),
            _gameLog, _gameLogLock,
            new Dictionary<string, string?>
            {
                ["ConnectionStrings__FenrirDb"] = ConnectionString,
                ["Game__Port"] = GamePort.ToString(),
                ["Game__ShardId"] = ShardId.ToString()
            });
        await WaitForServerReadyAsync(_gameProcess, GamePort, "GameServer", _gameLog, _gameLogLock);

        // GameServerDirectoryHeartbeat writes its first row as soon as the hosted service starts (do/while, no
        // initial delay), but that race isn't ordered against our own TCP-readiness probe above -- a short
        // grace window avoids ZoneTransferHandler seeing an empty runtime.GameServerDirectory on the very first try.
        await Task.Delay(TimeSpan.FromSeconds(2));

        TestAccountId = accountId;
    }

    public int TestAccountId { get; private set; }

    public async Task DisposeAsync()
    {
        TryKill(_gameProcess);
        TryKill(_loginProcess);

        if (_app is not null)
            await _app.DisposeAsync();
    }

    public async Task<SqlConnection> OpenConnectionAsync()
    {
        var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>
    ///     A freshly-"healthy" SQL Server container can still drop the very first heavy burst of DDL
    ///     (SqlException/transport-level connection reset) before it's genuinely stable -- retries with a whole
    ///     fresh container rather than resuming mid-manifest, since CREATE TABLE isn't safe to replay.
    /// </summary>
    private async Task<int> StartDatabaseWithRetryAsync()
    {
        const int maxAttempts = 3;

        for (var attempt = 1;; attempt++)
        {
            var builder = DistributedApplicationTestingBuilder.Create(NoArgs);
            builder.AddSqlServer("sqlserver").WithImageTag("2025-latest").AddDatabase("FenrirDbIntegration");
            _app = await builder.BuildAsync();

            try
            {
                await _app.StartAsync();

                using (var readyCts = new CancellationTokenSource(TimeSpan.FromMinutes(3)))
                    await _app.ResourceNotifications.WaitForResourceHealthyAsync("FenrirDbIntegration",
                        readyCts.Token);

                ConnectionString = await _app.GetConnectionStringAsync("FenrirDbIntegration") ??
                                    throw new InvalidOperationException(
                                        "The \"FenrirDbIntegration\" resource did not produce a connection " +
                                        "string even though it just reported healthy.");

                await Task.Delay(TimeSpan.FromSeconds(5));
                await ApplyManifestAsync();
                await SeedSecondShardMapAsync();
                return await SeedTestAccountAsync();
            }
            catch (SqlException) when (attempt < maxAttempts)
            {
                await _app.DisposeAsync();
                _app = null;
            }
        }
    }

    /// <summary>
    ///     Applies every script in Database/_manifest.txt (mirrors Fenrir.Tools.DbMigrator's batch-splitting, same
    ///     pattern as Fenrir.Data.Tests.Fixtures.SqlServerFixture).
    /// </summary>
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

    /// <summary>
    ///     M1's shipped seed (Database/70_seed/admin/005_shard_map_assignments.sql) only assigns map 1 to shard
    ///     1 -- deliberately so, per its own comment ("M1's single-shard default"). The plan's mandated scenario
    ///     needs a second in-process map for <c>ZoneMoveHandler</c>'s intra-shard transfer (ADR-0012), so this
    ///     test-only row is added on top, before the GameServer process ever reads admin.ShardMapAssignments
    ///     (read once at boot, see Servers/Fenrir.GameServer/Program.cs).
    /// </summary>
    private async Task SeedSecondShardMapAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT INTO admin.ShardMapAssignments (ShardId, MapId) VALUES (@ShardId, @MapId);", connection);
        command.Parameters.AddWithValue("@ShardId", ShardId);
        command.Parameters.AddWithValue("@MapId", SecondaryMapId);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    ///     There is no wire-level "create account" opcode in this game (real TS25 accounts are provisioned out
    ///     of band, e.g. a web portal) -- Login's own 13 incoming opcodes only ever authenticate an existing
    ///     account. So the test account is seeded directly, hashed with the exact same
    ///     <see cref="PasswordHasher" /> LoginHandler verifies against.
    /// </summary>
    private async Task<int> SeedTestAccountAsync()
    {
        var (hash, salt) = PasswordHasher.Hash(TestAccountPassword);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT INTO auth.Accounts (LoginName, PasswordHash, PasswordSalt) OUTPUT INSERTED.AccountId " +
            "VALUES (@LoginName, @PasswordHash, @PasswordSalt);", connection);
        command.Parameters.AddWithValue("@LoginName", TestAccountLoginName);
        command.Parameters.AddWithValue("@PasswordHash", hash);
        command.Parameters.AddWithValue("@PasswordSalt", salt);

        var accountId = (int)(await command.ExecuteScalarAsync())!;
        return accountId;
    }

    /// <summary>
    ///     This test project implicitly references the AspNetCore shared framework (via Aspire.Hosting.Testing),
    ///     so its own build omits Microsoft.Extensions.Hosting.dll from its output (assumed supplied by that shared
    ///     framework) -- which also strips it from the copied Fenrir.LoginServer.dll/Fenrir.GameServer.dll sitting
    ///     alongside it, even though those servers' own runtimeconfig.json only declares Microsoft.NETCore.App and
    ///     has no such fallback. Launching from each server's own original build output directory (same
    ///     Configuration/TFM as this test run) avoids the mismatch entirely.
    /// </summary>
    private static string OriginalBuildOutputDllPath(Assembly serverAssembly)
    {
        var tfmDir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var repoRoot = tfmDir.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
        var assemblyName = serverAssembly.GetName().Name!;
        return Path.Combine(repoRoot, "Servers", assemblyName, "bin", tfmDir.Parent.Name, tfmDir.Name,
            assemblyName + ".dll");
    }

    private static Process StartServerProcess(string assemblyDllPath, StringBuilder log, Lock logLock,
        IReadOnlyDictionary<string, string?> environment)
    {
        var startInfo = new ProcessStartInfo("dotnet", $"\"{assemblyDllPath}\"")
        {
            WorkingDirectory = Path.GetDirectoryName(assemblyDllPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var (key, value) in environment)
            startInfo.Environment[key] = value;

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is null) return;
            lock (logLock) log.AppendLine(args.Data);
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is null) return;
            lock (logLock) log.AppendLine(args.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static async Task WaitForServerReadyAsync(Process process, int port, string label, StringBuilder log,
        Lock logLock, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(90));
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
                throw new InvalidOperationException(
                    $"{label} process exited early (code {process.ExitCode}) before its port ({port}) ever " +
                    $"became reachable. Captured output:\n{Snapshot(log, logLock)}");

            try
            {
                using var probe = new TcpClient();
                await probe.ConnectAsync(IPAddress.Loopback, port).WaitAsync(TimeSpan.FromSeconds(2));
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(300));
            }
        }

        throw new TimeoutException(
            $"{label} did not become reachable on port {port} within {timeout}. Captured output:\n" +
            $"{Snapshot(log, logLock)}", lastError);
    }

    private static string Snapshot(StringBuilder log, Lock logLock)
    {
        lock (logLock)
            return log.ToString();
    }

    private static int ReserveEphemeralLoopbackPort()
    {
        using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        probe.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.LocalEndPoint!).Port;
    }

    private static void TryKill(Process? process)
    {
        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort: the process may have already exited between the check and the kill.
        }
        finally
        {
            process.Dispose();
        }
    }
}
