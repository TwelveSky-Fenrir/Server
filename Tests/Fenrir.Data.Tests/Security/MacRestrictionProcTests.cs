using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Security;
using Fenrir.Data.Security;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Security;

// admin.usp_MacRestriction_* against real SQL Server 2025, driven through MacRestrictionRepository -- exercises
// the MAC-wide-default-vs-per-install-override precedence documented on IMacRestrictionRepository.
// MacRestrictionRepository caches GetAll for 2s (CaeriusNet's cache is process-wide static storage, not scoped
// per test -- see GameServerDirectoryRepositoryTests), so every assertion that must see a just-inserted row
// waits past that TTL first.
[Collection("SqlServer")]
public class MacRestrictionProcTests
{
    private static readonly TimeSpan CacheBypassDelay = TimeSpan.FromSeconds(3);

    private readonly string _connectionString;
    private readonly IMacRestrictionRepository _restrictions;

    public MacRestrictionProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _restrictions = new MacRestrictionRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task IsBannedAsync_UnknownMac_IsFalse()
    {
        var mac = UniqueMac();

        Assert.False(await _restrictions.IsBannedAsync(mac, "some-adapter-guid", CancellationToken.None));
    }

    [Fact]
    public async Task IsBannedAsync_MacWideDefaultRowWithZeroLimit_BansEveryInstall()
    {
        var mac = UniqueMac();
        await AddAsync(mac, null, 0);
        await Task.Delay(CacheBypassDelay);

        Assert.True(await _restrictions.IsBannedAsync(mac, "adapter-a", CancellationToken.None));
        Assert.True(await _restrictions.IsBannedAsync(mac, "adapter-b", CancellationToken.None));
        Assert.True(await _restrictions.IsBannedAsync(mac, null, CancellationToken.None));
    }

    [Fact]
    public async Task IsBannedAsync_PerInstallOverride_TakesPrecedenceOverTheMacWideDefault()
    {
        var mac = UniqueMac();
        await AddAsync(mac, null, 0); // MAC banned by default...
        await AddAsync(mac, "trusted-adapter", 5); // ...except this one specific install.
        await Task.Delay(CacheBypassDelay);

        Assert.False(await _restrictions.IsBannedAsync(mac, "trusted-adapter", CancellationToken.None));
        Assert.True(await _restrictions.IsBannedAsync(mac, "some-other-adapter", CancellationToken.None));
    }

    [Fact]
    public async Task IsBannedAsync_PositiveAccountLimit_IsNotBanned()
    {
        var mac = UniqueMac();
        await AddAsync(mac, null, 2);

        Assert.False(await _restrictions.IsBannedAsync(mac, "adapter-a", CancellationToken.None));
    }

    [Fact]
    public async Task MacRestriction_Add_RejectsADuplicateMacAndMachineGuidPair()
    {
        var mac = UniqueMac();
        await AddAsync(mac, "adapter-a", 1);

        var duplicate = await Assert.ThrowsAsync<SqlException>(() => AddAsync(mac, "adapter-a", 1));
        Assert.Equal(50305, duplicate.Number);
    }

    private static string UniqueMac()
    {
        return $"00-{Guid.NewGuid():N}"[..17].ToUpperInvariant();
    }

    private async Task AddAsync(string macAddress, string? machineGuid, int accountLimit)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("admin.usp_MacRestriction_Add", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("MacAddress", macAddress);
        command.Parameters.AddWithValue("MachineGuid", (object?)machineGuid ?? DBNull.Value);
        command.Parameters.AddWithValue("AccountLimit", accountLimit);
        await command.ExecuteScalarAsync();
    }
}
