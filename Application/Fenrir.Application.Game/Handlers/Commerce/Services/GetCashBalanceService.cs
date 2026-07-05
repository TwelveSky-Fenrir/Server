using Fenrir.Data.Commerce;

namespace Fenrir.Application.Game.Handlers.Commerce.Services;

/// <summary>Business logic for CZ_GET_CASH_SIZE_SEND (opcode 41), extracted from <see cref="GetCashBalanceHandler" />.</summary>
public interface IGetCashBalanceService
{
    ValueTask<int> GetBalanceAsync(int accountId, CancellationToken cancellationToken);
}

public sealed class GetCashBalanceService(ICashRepository cash) : IGetCashBalanceService
{
    public async ValueTask<int> GetBalanceAsync(int accountId, CancellationToken cancellationToken)
    {
        return await cash.GetBalanceAsync(accountId, cancellationToken);
    }
}
