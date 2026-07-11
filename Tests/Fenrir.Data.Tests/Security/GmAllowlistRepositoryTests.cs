using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Security;
using Fenrir.Data.Security;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Security;

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

        var sqlException = ex as SqlException ?? ex!.InnerException as SqlException;
        if (sqlException is not null)
            Assert.Equal(50304, sqlException.Number);
    }
}
