using Fenrir.Application.Login.Abstractions.RenameAvatar;
using Fenrir.Domain.Login.Avatars;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Application.Login.Packets;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

public sealed class RenameAvatarHandler(IRenameAvatarService renameAvatarService, ILogger<RenameAvatarHandler> logger)
    : IAsyncPacketHandler<RenameAvatarRequest>
{
    private const int MaxAvatarPost = 2;
    private const int InventoryPageCount = 2;
    private const int InventorySlotCount = 64;

    public async ValueTask HandleAsync(RenameAvatarRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId}: op19 CL_CHANGE_AVATAR_NAME_SEND received for account {AccountId} slot {Slot}",
                session.SessionId, accountId, packet.AvatarPost);

        if (packet.AvatarPost is < 0 or > MaxAvatarPost ||
            packet.ChangeAvatarName.Length == 0 ||
            packet.Page is < 0 or >= InventoryPageCount ||
            packet.Index is < 0 or >= InventorySlotCount ||
            !AvatarNameValidator.HasOnlyWhitelistedCharacters(packet.ChangeAvatarName))
        {
            logger.LogWarning(
                "Avatar rename rejected: malformed request from account {AccountId} (slot {Slot}) -- aborting",
                accountId, packet.AvatarPost);
            loginSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var result = await renameAvatarService.RenameAvatarAsync(accountId, (byte)packet.AvatarPost,
            packet.ChangeAvatarName, (byte)packet.Page, (byte)packet.Index, cancellationToken);

        switch (result.Outcome)
        {
            case RenameAvatarOutcome.SlotEmpty:
                logger.LogWarning(
                    "Avatar rename rejected: malformed request from account {AccountId} (slot {Slot} is empty)",
                    accountId, packet.AvatarPost);
                loginSession.Abort(DisconnectReason.Malformed);
                return;
            case RenameAvatarOutcome.Success:
                logger.LogInformation("Avatar renamed: account {AccountId} slot {Slot} -> {NewName}", accountId,
                    packet.AvatarPost, packet.ChangeAvatarName);
                session.Send(new RenameAvatarResponse { Result = 0 });
                return;
            case RenameAvatarOutcome.NameTaken:
                logger.LogInformation(
                    "Avatar rename rejected: account {AccountId} slot {Slot} -- name unchanged/taken", accountId,
                    packet.AvatarPost);
                session.Send(new RenameAvatarResponse { Result = 2 });
                return;
            case RenameAvatarOutcome.TribeRoleRefusal:
            case RenameAvatarOutcome.GuildMembershipRefusal:
            case RenameAvatarOutcome.FriendListRefusal:
            case RenameAvatarOutcome.TeacherBondRefusal:
            case RenameAvatarOutcome.StudentBondRefusal:
                logger.LogWarning(
                    "Avatar rename refused: account {AccountId} slot {Slot} outcome {Outcome}", accountId,
                    packet.AvatarPost, result.Outcome);
                session.Send(new RenameAvatarResponse { Result = 3 });
                return;
            case RenameAvatarOutcome.SqlError:
                logger.LogWarning("Avatar rename failed: account {AccountId} slot {Slot} (SQL error)", accountId,
                    packet.AvatarPost);
                session.Send(new RenameAvatarResponse { Result = 101 });
                return;
            case RenameAvatarOutcome.SlotMissing:
                logger.LogWarning(
                    "Avatar rename rejected: account {AccountId} slot {Slot} vanished mid-transaction", accountId,
                    packet.AvatarPost);
                session.Send(new RenameAvatarResponse { Result = 102 });
                return;
            case RenameAvatarOutcome.ItemMismatch:
                logger.LogWarning(
                    "Avatar rename rejected: account {AccountId} slot {Slot} -- rename scroll not found at the claimed slot -- aborting",
                    accountId, packet.AvatarPost);
                loginSession.Abort(DisconnectReason.Malformed);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, null);
        }
    }
}
