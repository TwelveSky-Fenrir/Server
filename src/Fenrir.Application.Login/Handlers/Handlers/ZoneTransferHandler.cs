using Fenrir.Application.Login.Abstractions.ZoneTransfer;
using Fenrir.Application.Login.Sessions;
using Fenrir.Protocol.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

public sealed class ZoneTransferHandler(IZoneTransferService zoneTransferService, ILogger<ZoneTransferHandler> logger)
    : IAsyncPacketHandler<ZoneTransferRequest>
{
    private const int MaxAvatarPost = 2;

    private const int ClosedZonePort = 0;

    public async ValueTask HandleAsync(ZoneTransferRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        if (packet.AvatarPost is < 0 or > MaxAvatarPost)
        {
            logger.LogWarning(
                "Zone transfer rejected: malformed request from account {AccountId} (slot {Slot})", accountId,
                packet.AvatarPost);
            loginSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var sessionToken = loginSession.AccountSessionToken!.Value;

        var result = await zoneTransferService.RequestZoneTransferAsync(accountId, (byte)packet.AvatarPost,
            sessionToken, loginSession.AccountGrade, cancellationToken);

        switch (result.Outcome)
        {
            case ZoneTransferOutcome.SlotEmpty:
                logger.LogWarning(
                    "Zone transfer rejected: malformed request from account {AccountId} (slot {Slot} is empty)",
                    accountId, packet.AvatarPost);
                loginSession.Abort(DisconnectReason.Malformed);
                return;
            case ZoneTransferOutcome.CharacterNotFound:
            case ZoneTransferOutcome.ShardUnavailable:
                logger.LogWarning(
                    "Zone transfer rejected: account {AccountId} slot {Slot} outcome {Outcome}", accountId,
                    packet.AvatarPost, result.Outcome);
                session.Send(new ZoneTransferResponse
                    { Result = 1, Ip = result.Ip, Port = ClosedZonePort, Zone = result.Zone });
                return;
            case ZoneTransferOutcome.Success:
                loginSession.MarkHandoverIssued();

                logger.LogInformation(
                    "Zone transfer granted: account {AccountId} slot {Slot} -> {Ip}:{Port} (MapId {MapId})",
                    accountId, packet.AvatarPost, result.Ip, result.Port, result.Zone);

                session.Send(new ZoneTransferResponse
                    { Result = 0, Ip = result.Ip, Port = result.Port, Zone = result.Zone });
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, null);
        }
    }
}
