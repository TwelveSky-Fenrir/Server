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
public class FirewallProcTests
{
    private static readonly TimeSpan CacheBypassDelay = TimeSpan.FromSeconds(3);

    private readonly IBlockedIpRepository _blockedIps;
    private readonly string _connectionString;
    private readonly IFirewallRuleRepository _firewallRules;
    private readonly IGmAllowlistRepository _gmAllowlist;

    public FirewallProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _blockedIps = new BlockedIpRepository(db);
        _firewallRules = new FirewallRuleRepository(db);
        _gmAllowlist = new GmAllowlistRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task BlockedIp_IsBlockedAsync_TrueOnlyAfterBeingAdded()
    {
        var ip = UniqueIp();

        Assert.False(await _blockedIps.IsBlockedAsync(ip, CancellationToken.None));

        await ExecProcAsync("admin.usp_BlockedIp_Add", ("IpAddress", ip));

        Assert.True(await _blockedIps.IsBlockedAsync(ip, CancellationToken.None));
    }

    [Fact]
    public async Task BlockedIp_Add_RejectsADuplicateIp()
    {
        var ip = UniqueIp();
        await ExecProcAsync("admin.usp_BlockedIp_Add", ("IpAddress", ip));

        var duplicate = await Assert.ThrowsAsync<SqlException>(() =>
            ExecProcAsync("admin.usp_BlockedIp_Add", ("IpAddress", ip)));
        Assert.Equal(50302, duplicate.Number);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)3)]
    public async Task FirewallRule_IsBlockedAsync_TrueForBlockRuleTypes(byte blockRuleType)
    {
        var ip = UniqueIp();
        await ExecProcAsync("admin.usp_FirewallRule_Add", ("IpAddress", ip), ("RuleType", blockRuleType));
        await Task.Delay(CacheBypassDelay);

        Assert.True(await _firewallRules.IsBlockedAsync(ip, CancellationToken.None));
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)2)]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    public async Task FirewallRule_IsBlockedAsync_FalseForAllowRuleTypes(byte allowRuleType)
    {
        var ip = UniqueIp();
        await ExecProcAsync("admin.usp_FirewallRule_Add", ("IpAddress", ip), ("RuleType", allowRuleType));

        Assert.False(await _firewallRules.IsBlockedAsync(ip, CancellationToken.None));
    }

    [Fact]
    public async Task GmAllowlist_IsAllowedAsync_TrueOnlyAfterBeingAdded()
    {
        var ip = UniqueIp();

        Assert.False(await _gmAllowlist.IsAllowedAsync(ip, CancellationToken.None));

        await ExecProcAsync("admin.usp_GmAllowlist_Add", ("IpAddress", ip));
        await Task.Delay(CacheBypassDelay);

        Assert.True(await _gmAllowlist.IsAllowedAsync(ip, CancellationToken.None));
    }

        private static string UniqueIp()
    {
        return $"203.0.113.{Guid.NewGuid():N}"[..30];
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
