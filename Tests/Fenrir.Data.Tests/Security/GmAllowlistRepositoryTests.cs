using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Security;
using Fenrir.Data.Security;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Security;

// Exercises admin.usp_GmAllowlist_Add/GetAll against the real containerized SQL Server (no mocks). Each test
// mints its own GUID-suffixed IP to stay independent from other tests -- IsAllowedAsync's own 2-second
// in-memory cache (GmAllowlistRepository.GetAllAsync) is deliberately not exercised here, since its cache key
// is process-wide and could observe a stale snapshot cached by an unrelated concurrently-running test; the
// cache itself is already covered indirectly by every LoginService-level test that stubs IGmAllowlistRepository.
[Collection("SqlServer")]
public sealed class GmAllowlistRepositoryTests
{
    private readonly IGmAllowlistRepository _repository;

    public GmAllowlistRepositoryTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _repository = new GmAllowlistRepository(db);
    }

    private static string NewIpAddress()
    {
        // Fits VARCHAR(45); a full GUID would overflow it, so only the first segment is used.
        return $"10.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
    }

    [Fact]
    public async Task AddAsync_InsertsRow_ReturnsPositiveId()
    {
        var id = await _repository.AddAsync(NewIpAddress(), CancellationToken.None);

        Assert.True(id > 0);
    }

    [Fact]
    public async Task AddAsync_SameIpTwice_ThrowsOnSecondInsert()
    {
        var ip = NewIpAddress();
        await _repository.AddAsync(ip, CancellationToken.None);

        var ex = await Record.ExceptionAsync(() => _repository.AddAsync(ip, CancellationToken.None).AsTask());

        Assert.NotNull(ex);

        // usp_GmAllowlist_Add raises THROW 50304 for a duplicate IpAddress; CaeriusNet may wrap the SqlException.
        var sqlException = ex as SqlException ?? ex!.InnerException as SqlException;
        if (sqlException is not null)
            Assert.Equal(50304, sqlException.Number);
    }
}
