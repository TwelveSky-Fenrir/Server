using Fenrir.Application.Game.Abstractions.Commerce;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class GetCashBalanceService(ICashRepository cash, ILogger<GetCashBalanceService> logger)
    : IGetCashBalanceService
{
    public async ValueTask<int> GetBalanceAsync(int accountId, CancellationToken cancellationToken)
    {
        try
        {
            var balance = await cash.GetBalanceAsync(accountId, cancellationToken);
            logger.LogDebug("Get cash balance: account {AccountId} balance {Balance}", accountId, balance);
            return balance;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Account {AccountId} GetBalanceAsync failed -- substituting 0 balance to match legacy GET_CASH_SIZE_SEND",
                accountId);
            return 0;
        }
    }
}
