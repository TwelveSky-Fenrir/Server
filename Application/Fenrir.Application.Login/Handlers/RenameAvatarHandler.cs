using Fenrir.Application.Login.Handlers.Services;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op19 CL_CHANGE_AVATAR_NAME_SEND — result codes forwarded verbatim from game.usp_Character_Rename (0 ok, 2 name
///     taken/unchanged, 102 slot missing, 101 SQL error).
/// </summary>
/// <remarks>
///     Rename-scroll item 1133 not verified/consumed (no character inventory persistence yet); tribe/guild/friend/teacher
///     Result=3 refusal not reproduced (those relations don't exist for login-visible characters yet).
/// </remarks>
public sealed class RenameAvatarHandler(IRenameAvatarService renameAvatarService)
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

        // Legacy Quit() guards (S04_MyWork02.cpp l.1310-1348).
        if (packet.AvatarPost is < 0 or > MaxAvatarPost ||
            packet.ChangeAvatarName.Length == 0 ||
            packet.Page is < 0 or >= InventoryPageCount ||
            packet.Index is < 0 or >= InventorySlotCount)
        {
            loginSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var result = await renameAvatarService.RenameAvatarAsync(accountId, (byte)packet.AvatarPost,
            packet.ChangeAvatarName, cancellationToken);

        session.Send(new RenameAvatarResponse { Result = result });
    }
}
