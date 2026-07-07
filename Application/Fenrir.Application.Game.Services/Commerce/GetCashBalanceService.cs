using Fenrir.Application.Game.Abstractions.Commerce;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class GetCashBalanceService(ICashRepository cash, ILogger<GetCashBalanceService> logger)
    : IGetCashBalanceService
{
    /// <summary>
    ///     Legacy CZ_GET_CASH_SIZE_SEND / GET_CASH_SIZE_SEND never fails the request on a DB/IPC error --
    ///     it substitutes a 0 balance and still answers the client rather than dropping the connection.
    ///     SessionLoop's own dispatch-level catch treats *any* uncaught handler exception as fatal
    ///     (DisconnectReason.Faulted, connection torn down), which is the right default for genuine bugs but
    ///     wrong for this specific opcode's documented failure posture -- so the substitution has to happen
    ///     here, not be left to the dispatcher.
    /// </summary>
    public async ValueTask<int> GetBalanceAsync(int accountId, CancellationToken cancellationToken)
    {
        try
        {
            return await cash.GetBalanceAsync(accountId, cancellationToken);
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
