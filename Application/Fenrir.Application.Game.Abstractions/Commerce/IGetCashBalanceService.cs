namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>Business logic for CZ_GET_CASH_SIZE_SEND (opcode 41), extracted from <see cref="GetCashBalanceHandler" />.</summary>
public interface IGetCashBalanceService
{
    public ValueTask<int> GetBalanceAsync(int accountId, CancellationToken cancellationToken);
}
