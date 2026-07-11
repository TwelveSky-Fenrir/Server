using Fenrir.Application.Game.Services.Commerce;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Commerce;

public class GetCashBalanceServiceTests
{
    [Fact]
    public async Task GetBalanceAsync_ReturnsTheRepositoryBalance_WhenTheReadSucceeds()
    {
        var repository = new FakeCashRepository();
        repository.SeedBalance(7, 12_345);
        var service = new GetCashBalanceService(repository, NullLogger<GetCashBalanceService>.Instance);

        var balance = await service.GetBalanceAsync(7, CancellationToken.None);

        Assert.Equal(12_345, balance);
    }

    [Fact]
    public async Task GetBalanceAsync_SubstitutesZero_WhenTheRepositoryThrows()
    {
        var repository = new FakeCashRepository { ThrowOnGetBalance = true };
        repository.SeedBalance(7, 12_345);
        var service = new GetCashBalanceService(repository, NullLogger<GetCashBalanceService>.Instance);

        var balance = await service.GetBalanceAsync(7, CancellationToken.None);

        Assert.Equal(0, balance);
    }
}
