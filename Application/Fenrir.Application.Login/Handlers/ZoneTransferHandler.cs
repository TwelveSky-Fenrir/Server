using Fenrir.Application.Login.Handlers.Services;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op22 CL_DEMAND_ZONE_SERVER_INFO_SEND — login-to-zone handover; the handover identity lives server-side in the
///     ticket row, never on the wire.
/// </summary>
public sealed class ZoneTransferHandler(IZoneTransferService zoneTransferService)
    : IAsyncPacketHandler<ZoneTransferRequest>
{
    public async ValueTask HandleAsync(ZoneTransferRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        var result = await zoneTransferService.RequestZoneTransferAsync(accountId, (byte)packet.AvatarPost,
            cancellationToken);

        if (result.Outcome != ZoneTransferOutcome.Success)
        {
            // Result=1 ("zone fermee") reused for lack of a more specific documented code (wire contract §4.8).
            session.Send(new ZoneTransferResponse { Result = 1, Ip = "", Port = 0, Zone = 0 });
            return;
        }

        loginSession.MarkHandoverIssued();

        session.Send(new ZoneTransferResponse
            { Result = 0, Ip = result.Ip, Port = result.Port, Zone = result.Zone });
    }
}
