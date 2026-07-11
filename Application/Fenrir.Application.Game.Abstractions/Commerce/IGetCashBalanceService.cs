namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface IGetCashBalanceService
{
    public ValueTask<int> GetBalanceAsync(int accountId, CancellationToken cancellationToken);
}
