using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Security;
using Fenrir.Data.Security;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Security;

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
    [InlineData((byte)0)]
    [InlineData((byte)2)]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    public async Task ReconcileAllowlistAsync_DeletesEveryAllowDesignatedRow(byte allowRuleType)
    {
        var ip = UniqueIp();
        await ExecProcAsync("admin.usp_FirewallRule_Add", ("IpAddress", ip), ("RuleType", allowRuleType));
        Assert.True(await RowExistsAsync(ip));

        await _firewallRules.ReconcileAllowlistAsync(CancellationToken.None);

        Assert.False(await RowExistsAsync(ip));
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)3)]
    public async Task ReconcileAllowlistAsync_LeavesBlockDesignatedRowsUntouched(byte blockRuleType)
    {
        var ip = UniqueIp();
        await ExecProcAsync("admin.usp_FirewallRule_Add", ("IpAddress", ip), ("RuleType", blockRuleType));
        Assert.True(await RowExistsAsync(ip));

        await _firewallRules.ReconcileAllowlistAsync(CancellationToken.None);

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
        var blockIp = UniqueIp();
        await ExecProcAsync("admin.usp_FirewallRule_Add", ("IpAddress", blockIp), ("RuleType", (byte)3));

        await _firewallRules.ReconcileAllowlistAsync(CancellationToken.None);

        Assert.True(await RowExistsAsync(blockIp));
    }

        private static string UniqueIp()
    {
        return $"203.0.113.{Guid.NewGuid():N}"[..30];
    }

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
