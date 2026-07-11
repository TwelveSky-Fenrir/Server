using Fenrir.Application.Login.Abstractions.ZoneTransfer;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

public sealed class ZoneTransferHandler(IZoneTransferService zoneTransferService, ILogger<ZoneTransferHandler> logger)
    : IAsyncPacketHandler<ZoneTransferRequest>
{
    public async ValueTask HandleAsync(ZoneTransferRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        var sessionToken = loginSession.AccountSessionToken!.Value;

        var result = await zoneTransferService.RequestZoneTransferAsync(accountId, (byte)packet.AvatarPost,
            sessionToken, loginSession.AccountGrade, cancellationToken);

        if (result.Outcome != ZoneTransferOutcome.Success)
        {
            logger.LogWarning(
                "Zone transfer rejected: account {AccountId} slot {Slot} outcome {Outcome}", accountId,
                packet.AvatarPost, result.Outcome);
            session.Send(new ZoneTransferResponse { Result = 1, Ip = "", Port = 0, Zone = 0 });
            return;
        }

        loginSession.MarkHandoverIssued();

        logger.LogInformation(
            "Zone transfer granted: account {AccountId} slot {Slot} -> {Ip}:{Port} (MapId {MapId})", accountId,
            packet.AvatarPost, result.Ip, result.Port, result.Zone);

        session.Send(new ZoneTransferResponse
            { Result = 0, Ip = result.Ip, Port = result.Port, Zone = result.Zone });
    }
}
