using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Commerce;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_GET_CASH_SIZE_SEND (opcode 41, contracts/04_commerce.md) -- reads the account's real-money cash
///     balance (game.AccountCash, already scaffolded phase A3). <c>Sort</c> is a pure client-side UI
///     routing echo, never inspected server-side (verified). No SQL failure path exists worth modeling: a
///     never-credited account already reads as balance 0 (usp_Cash_GetBalance's own contract), the exact
///     same "IPC failure -&gt; balance 0, no error" answer the legacy's own ts25extra round trip gave.
/// </summary>
public sealed class GetCashBalanceHandler(CashRepository cash) : IAsyncPacketHandler<GetCashBalanceRequest>
{
    public async ValueTask HandleAsync(GetCashBalanceRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var accountId = zoneSession.AccountId!.Value;

        var balance = await cash.GetBalanceAsync(accountId, cancellationToken);

        session.Send(new GetCashBalanceResponse { CashSize = balance, Sort = packet.Sort });
    }
}
