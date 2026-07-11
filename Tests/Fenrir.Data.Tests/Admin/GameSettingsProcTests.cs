using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Admin;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Admin;

[Collection("SqlServer")]
public class GameSettingsProcTests
{
    private readonly IGameSettingsRepository _repository;

    public GameSettingsProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _repository = new GameSettingsRepository(db);
    }

    [Fact]
    public async Task GetAsync_ReturnsSeededSingletonRow()
    {
        var settings = await _repository.GetAsync(CancellationToken.None);

        Assert.Equal(7, settings.ProxyShopDurationDays);
    }
}
