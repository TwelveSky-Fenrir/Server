using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Security;
using Fenrir.Data.Security;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Security;

/// <summary>
///     Workstream D3 -- <c>admin.usp_FirewallRule_ReconcileAllowlist</c> against real SQL Server 2025, driven
///     through <see cref="FirewallRuleRepository.ReconcileAllowlistAsync" />. See that method's own remarks
///     for exactly which of legacy <c>ts25firewall</c>'s three <c>RemoveIPTick</c> reconcile sub-steps this
///     covers (prune every ALLOW-designated row unconditionally -- Fenrir tracks no per-account "current IP"
///     roster to exempt any of them, unlike legacy) and which two it deliberately does not reproduce (the
///     11-fixed-infra-IP reseed; the per-account IP resync).
/// </summary>
[Collection("SqlServer")]
public class FirewallAllowlistReconcileProcTests
{
    private readonly string _connectionString;
    private readonly IFirewallRuleRepository _firewallRules;

    public FirewallAllowlistReconcileProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _firewallRules = new FirewallRuleRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Theory]
    [InlineData((byte)0)] // TCP_ALLOW
    [InlineData((byte)2)] // ANY_ALLOW
    [InlineData((byte)4)] // TCP_ALLOW_CF
    [InlineData((byte)5)] // TCP_ALLOW_IPRANGE
    public async Task ReconcileAllowlistAsync_DeletesEveryAllowDesignatedRow(byte allowRuleType)
    {
        var ip = UniqueIp();
        await ExecProcAsync("admin.usp_FirewallRule_Add", ("IpAddress", ip), ("RuleType", allowRuleType));
        Assert.True(await RowExistsAsync(ip));

        await _firewallRules.ReconcileAllowlistAsync(CancellationToken.None);

        Assert.False(await RowExistsAsync(ip));
    }

    [Theory]
    [InlineData((byte)1)] // TCP_BLOCK
    [InlineData((byte)3)] // ANY_BLOCK
    public async Task ReconcileAllowlistAsync_LeavesBlockDesignatedRowsUntouched(byte blockRuleType)
    {
        var ip = UniqueIp();
        await ExecProcAsync("admin.usp_FirewallRule_Add", ("IpAddress", ip), ("RuleType", blockRuleType));
        Assert.True(await RowExistsAsync(ip));

        await _firewallRules.ReconcileAllowlistAsync(CancellationToken.None);

        // Structurally cannot be deleted: the procedure's WHERE clause only ever matches the allow designations.
        Assert.True(await RowExistsAsync(ip));
    }

    [Fact]
    public async Task ReconcileAllowlistAsync_MixedRows_OnlyAllowRowsArePruned()
    {
        var allowIp = UniqueIp();
        var blockIp = UniqueIp();
        await ExecProcAsync("admin.usp_FirewallRule_Add", ("IpAddress", allowIp), ("RuleType", (byte)0));
        await ExecProcAsync("admin.usp_FirewallRule_Add", ("IpAddress", blockIp), ("RuleType", (byte)3));

        await _firewallRules.ReconcileAllowlistAsync(CancellationToken.None);

        Assert.False(await RowExistsAsync(allowIp));
        Assert.True(await RowExistsAsync(blockIp));
    }

    [Fact]
    public async Task ReconcileAllowlistAsync_NoAllowRowsPresent_IsANoOp()
    {
        // Guards against an over-broad WHERE clause: with nothing to prune, the call must not throw and must
        // not touch unrelated rows.
        var blockIp = UniqueIp();
        await ExecProcAsync("admin.usp_FirewallRule_Add", ("IpAddress", blockIp), ("RuleType", (byte)3));

        await _firewallRules.ReconcileAllowlistAsync(CancellationToken.None);

        Assert.True(await RowExistsAsync(blockIp));
    }

    /// <summary>
    ///     A syntactically fake but always-unique value: IpAddress only needs to be a stable key across this
    ///     table's UNIQUE constraint, never a parsed/validated address -- same convention as
    ///     <c>FirewallProcTests.UniqueIp</c>.
    /// </summary>
    private static string UniqueIp()
    {
        return $"203.0.113.{Guid.NewGuid():N}"[..30];
    }

    /// <summary>Bypasses FirewallRuleRepository's 2s in-memory GetAll cache entirely -- a direct row-existence probe.</summary>
    private async Task<bool> RowExistsAsync(string ipAddress)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command =
            new SqlCommand("SELECT COUNT(1) FROM admin.FirewallRules WHERE IpAddress = @IpAddress", connection);
        command.Parameters.AddWithValue("@IpAddress", ipAddress);
        var count = (int)(await command.ExecuteScalarAsync())!;
        return count > 0;
    }

    private async Task ExecProcAsync(string procName, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(procName, connection) { CommandType = CommandType.StoredProcedure };
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }
}
