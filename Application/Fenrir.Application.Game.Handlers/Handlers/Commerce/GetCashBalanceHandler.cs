using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class GetCashBalanceHandler(IGetCashBalanceService service, ILogger<GetCashBalanceHandler> logger)
    : IAsyncPacketHandler<GetCashBalanceRequest>
{
    public async ValueTask HandleAsync(GetCashBalanceRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var accountId = zoneSession.AccountId!.Value;

        logger.LogDebug("GetCashBalance: session {SessionId} account {AccountId} sort {Sort}", session.SessionId,
            accountId, packet.Sort);

        var balance = await service.GetBalanceAsync(accountId, cancellationToken);

        session.Send(new GetCashBalanceResponse { CashSize = balance, Sort = packet.Sort });
    }
}
