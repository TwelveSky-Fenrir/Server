using Fenrir.Application.Game.Abstractions.Commerce;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class GetCashBalanceService(ICashRepository cash) : IGetCashBalanceService
{
    public async ValueTask<int> GetBalanceAsync(int accountId, CancellationToken cancellationToken)
    {
        return await cash.GetBalanceAsync(accountId, cancellationToken);
    }
}
