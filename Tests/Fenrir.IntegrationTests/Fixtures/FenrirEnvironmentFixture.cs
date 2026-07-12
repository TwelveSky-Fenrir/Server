using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Aspire.Hosting.Testing;
using Fenrir.Data.Security;
using Microsoft.Data.SqlClient;

namespace Fenrir.IntegrationTests.Fixtures;

public sealed class FenrirEnvironmentFixture : IAsyncLifetime
{
    public const byte ShardId = 1;
    public const short PrimaryMapId = 1;
    public const short SecondaryMapId = 2;

    public const byte ShardId2 = 2;

    public const string TestAccountLoginName = "e2ebot";
    public const string TestAccountPassword = "E2E-bot-p4ssw0rd!";

    public const string TestAccountLoginName2 = "e2ebot-shard2";
    public const string TestAccountPassword2 = "E2E-bot-shard2-p4ssw0rd!";

    private static readonly string[] NoArgs = [];
    private readonly StringBuilder _gameLog = new();
    private readonly StringBuilder _gameLog2 = new();
    private readonly Lock _gameLogLock = new();
    private readonly Lock _gameLogLock2 = new();
    private readonly StringBuilder _loginLog = new();
    private readonly Lock _loginLogLock = new();

    private DistributedApplication? _app;
    private Process? _gameProcess;
    private Process? _gameProcess2;
    private Process? _loginProcess;

    public string ConnectionString { get; private set; } = string.Empty;
    public int LoginPort { get; private set; }
    public int GamePort { get; private set; }

    public int GamePort2 { get; private set; }

    public int TestAccountId { get; private set; }

    public int TestAccountId2 { get; private set; }

    public async Task InitializeAsync()
    {
        var (accountId, accountId2) = await StartDatabaseWithRetryAsync();

        LoginPort = ReserveEphemeralLoopbackPort();
        GamePort = ReserveEphemeralLoopbackPort();
        GamePort2 = ReserveEphemeralLoopbackPort();

        _loginProcess = StartServerProcess(
            OriginalBuildOutputDllPath("Fenrir.LoginServer"),
            _loginLog, _loginLogLock,
            new Dictionary<string, string?>
            {
                ["ConnectionStrings__FenrirDb"] = ConnectionString,
                ["Login__Port"] = LoginPort.ToString()
            });
        await WaitForServerReadyAsync(_loginProcess, LoginPort, "LoginServer", _loginLog, _loginLogLock);

        _gameProcess = StartServerProcess(
            OriginalBuildOutputDllPath("Fenrir.GameServer"),
            _gameLog, _gameLogLock,
            new Dictionary<string, string?>
            {
                ["ConnectionStrings__FenrirDb"] = ConnectionString,
                ["Game__Port"] = GamePort.ToString(),
                ["Game__ShardId"] = ShardId.ToString()
            });
        await WaitForServerReadyAsync(_gameProcess, GamePort, "GameServer", _gameLog, _gameLogLock);

        _gameProcess2 = StartServerProcess(
            OriginalBuildOutputDllPath("Fenrir.GameServer"),
            _gameLog2, _gameLogLock2,
            new Dictionary<string, string?>
            {
                ["ConnectionStrings__FenrirDb"] = ConnectionString,
                ["Game__Port"] = GamePort2.ToString(),
                ["Game__ShardId"] = ShardId2.ToString()
            });
        await WaitForServerReadyAsync(_gameProcess2, GamePort2, "GameServer(Shard2)", _gameLog2, _gameLogLock2);

        await Task.Delay(TimeSpan.FromSeconds(2));

        TestAccountId = accountId;
        TestAccountId2 = accountId2;
    }

    public async Task DisposeAsync()
    {
        TryKill(_gameProcess2);
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

    public string LoginServerLogSnapshot()
    {
        return Snapshot(_loginLog, _loginLogLock);
    }

    public string GameServerLogSnapshot()
    {
        return Snapshot(_gameLog, _gameLogLock);
    }

    public string GameServer2LogSnapshot()
    {
        return Snapshot(_gameLog2, _gameLogLock2);
    }

    private async Task<(int AccountId, int AccountId2)> StartDatabaseWithRetryAsync()
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
                {
                    await _app.ResourceNotifications.WaitForResourceHealthyAsync("FenrirDbIntegration",
                        readyCts.Token);
                }

                ConnectionString = await _app.GetConnectionStringAsync("FenrirDbIntegration") ??
                                   throw new InvalidOperationException(
                                       "The \"FenrirDbIntegration\" resource did not produce a connection " +
                                       "string even though it just reported healthy.");

                await Task.Delay(TimeSpan.FromSeconds(5));
                await ApplyManifestAsync();
                await SeedSecondShardMapAsync();
                await SeedGmAllowlistAsync();
                var accountId = await SeedTestAccountAsync(TestAccountLoginName, TestAccountPassword);
                var accountId2 = await SeedTestAccountAsync(TestAccountLoginName2, TestAccountPassword2);
                return (accountId, accountId2);
            }
            catch (SqlException) when (attempt < maxAttempts)
            {
                await _app.DisposeAsync();
                _app = null;
            }
        }
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

    private async Task SeedGmAllowlistAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT INTO admin.GmAllowlists (IpAddress) VALUES (@IpAddress);", connection);
        command.Parameters.AddWithValue("@IpAddress", IPAddress.Loopback.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private async Task<int> SeedTestAccountAsync(string loginName, string password)
    {
        var (hash, salt) = PasswordHasher.Hash(password);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT INTO auth.Accounts (LoginName, PasswordHash, PasswordSalt, AccountGrade) " +
            "OUTPUT INSERTED.AccountId VALUES (@LoginName, @PasswordHash, @PasswordSalt, 1);", connection);
        command.Parameters.AddWithValue("@LoginName", loginName);
        command.Parameters.AddWithValue("@PasswordHash", hash);
        command.Parameters.AddWithValue("@PasswordSalt", salt);

        var accountId = (int)(await command.ExecuteScalarAsync())!;
        return accountId;
    }

    private static string OriginalBuildOutputDllPath(string serverProjectName)
    {
        var tfmDir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var repoRoot = tfmDir.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
        return Path.Combine(repoRoot, "Servers", serverProjectName, "bin", tfmDir.Parent.Name, tfmDir.Name,
            serverProjectName + ".dll");
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
            lock (logLock)
            {
                log.AppendLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is null) return;
            lock (logLock)
            {
                log.AppendLine(args.Data);
            }
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
        {
            return log.ToString();
        }
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
                process.Kill(true);
        }
        catch
        {
        }
        finally
        {
            process.Dispose();
        }
    }
}
