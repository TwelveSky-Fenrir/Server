using Fenrir.Application.Game.Handlers.Commerce.Services;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_GET_CASH_SIZE_SEND (opcode 41) -- reads the account's cash balance. <c>Sort</c> is a pure
///     client-side UI routing echo, never inspected server-side.
/// </summary>
public sealed class GetCashBalanceHandler(IGetCashBalanceService service)
    : IAsyncPacketHandler<GetCashBalanceRequest>
{
    public async ValueTask HandleAsync(GetCashBalanceRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var accountId = zoneSession.AccountId!.Value;

        var balance = await service.GetBalanceAsync(accountId, cancellationToken);

        session.Send(new GetCashBalanceResponse { CashSize = balance, Sort = packet.Sort });
    }
}
