using Fenrir.Application.Login.Abstractions.DeleteAvatar;
using Fenrir.Application.Login.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

public sealed class DeleteAvatarHandler(IDeleteAvatarService deleteAvatarService, ILogger<DeleteAvatarHandler> logger)
    : IAsyncPacketHandler<DeleteAvatarRequest>
{
    private const int ModeDelete = 1;

    public async ValueTask HandleAsync(DeleteAvatarRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        if (packet.AvatarPost is < 0 or > 2 || packet.Unknow1 != ModeDelete)
        {
            logger.LogWarning(
                "Delete-avatar rejected: malformed request from account {AccountId} (slot {Slot}, mode {Mode})",
                accountId, packet.AvatarPost, packet.Unknow1);
            loginSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var result =
            await deleteAvatarService.DeleteAvatarAsync(accountId, (byte)packet.AvatarPost, cancellationToken);

        switch (result.Outcome)
        {
            case DeleteAvatarOutcome.SlotEmpty:
                logger.LogWarning(
                    "Delete-avatar rejected: malformed request from account {AccountId} (slot {Slot} is empty)",
                    accountId, packet.AvatarPost);
                loginSession.Abort(DisconnectReason.Malformed);
                return;
            case DeleteAvatarOutcome.Success:
                logger.LogInformation("Avatar deleted: account {AccountId} slot {Slot}", accountId,
                    packet.AvatarPost);
                session.Send(new DeleteAvatarResponse { Result = 0 });
                return;
            case DeleteAvatarOutcome.TribeRoleRefusal:
                logger.LogWarning("Delete-avatar refused: account {AccountId} slot {Slot} holds a tribe role",
                    accountId, packet.AvatarPost);
                session.Send(new DeleteAvatarResponse { Result = 2 });
                return;
            case DeleteAvatarOutcome.GuildMembershipRefusal:
                logger.LogWarning("Delete-avatar refused: account {AccountId} slot {Slot} is guilded", accountId,
                    packet.AvatarPost);
                session.Send(new DeleteAvatarResponse { Result = 3 });
                return;
            case DeleteAvatarOutcome.ProxyShopRefusal:
                logger.LogWarning(
                    "Delete-avatar refused: account {AccountId} slot {Slot} has a pending proxy shop", accountId,
                    packet.AvatarPost);
                session.Send(new DeleteAvatarResponse { Result = 5 });
                return;
            case DeleteAvatarOutcome.SqlError:
                logger.LogWarning("Delete-avatar failed: account {AccountId} slot {Slot} (SQL error)", accountId,
                    packet.AvatarPost);
                session.Send(new DeleteAvatarResponse { Result = 1 });
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, null);
        }
    }
}
