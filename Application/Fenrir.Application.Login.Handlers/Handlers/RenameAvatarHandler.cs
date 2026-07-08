using Fenrir.Application.Login.Abstractions.RenameAvatar;
using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op19 CL_CHANGE_AVATAR_NAME_SEND — result codes: 0 renamed, 2 name unchanged/taken, 3 tribe
///     role/guild/friend/teacher/student refusal (no further detail distinguishing which), 101 SQL error,
///     102 slot vanished. A rename-scroll (item 1133) mismatch at the claimed (Page, Index) slot gets no
///     response at all -- same silent-disconnect treatment as the structural/charset checks below.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:1310-1349 (slot/name/page/index precondition sequence
///     culminating in the <see cref="AvatarNameValidator" /> whitelist call at l.1345, then the item-1133
///     gate at l.1340-1344 -- both silent-disconnect outcomes) ; Server/Header/safestring.h:43-81
///     (CheckNameString itself) ; Server/ts25login/S04_MyWork02.cpp:1358-1385 (the five relationship
///     refusals, all collapsing to Result=3 -- see RenameAvatarService/AvatarRenameGate for the per-rule
///     citations and ordering).
/// </remarks>
public sealed class RenameAvatarHandler(IRenameAvatarService renameAvatarService, ILogger<RenameAvatarHandler> logger)
    : IAsyncPacketHandler<RenameAvatarRequest>
{
    private const int MaxAvatarPost = 2; // MAX_USER_AVATAR_NUM - 1
    private const int InventoryPageCount = 2; // MAX_INVENTORY_PAGE_NUM
    private const int InventorySlotCount = 64; // MAX_INVENTORY_SLOT_NUM

    public async ValueTask HandleAsync(RenameAvatarRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId}: op19 CL_CHANGE_AVATAR_NAME_SEND received for account {AccountId} slot {Slot}",
                session.SessionId, accountId, packet.AvatarPost);

        // Legacy Quit() guards (S04_MyWork02.cpp l.1310-1348).
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
                // The exception itself is already logged at RenameAvatarService; this is just the outcome summary.
                logger.LogWarning("Avatar rename failed: account {AccountId} slot {Slot} (SQL error)", accountId,
                    packet.AvatarPost);
                session.Send(new RenameAvatarResponse { Result = 101 });
                return;
            case RenameAvatarOutcome.SlotMissing:
                logger.LogWarning("Avatar rename rejected: account {AccountId} slot {Slot} holds no character",
                    accountId, packet.AvatarPost);
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
