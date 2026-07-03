using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op19 CL_CHANGE_AVATAR_NAME_SEND — character rename (login protocol report §4.19). Structural violations
///     reproduce the legacy Quit() as <see cref="ClientSession.Abort" /> (post/page/index out of bounds, empty
///     name); business failures reply with the LEGACY codes, produced by game.usp_Character_Rename and forwarded
///     verbatim: 0 renamed, 2 name taken (also covers "new == current", which the legacy answered 2 for as a
///     dedicated pre-check), 102 slot empty/vanished, plus 101 for any SQL-level error (the legacy catch-all).
///     Name validation is equivalent to avatar creation's: bounded to 13 bytes by the wire contract, non-empty
///     enforced here, uniqueness enforced by the proc — the same trio usp_Character_Create applies.
/// </summary>
/// <remarks>
///     Two legacy checks are deliberately deferred, not skipped silently:
///     <list type="bullet">
///         <item>
///             The rename scroll (item 1133 at aInventory[tPage][tIndex]) is not verified/consumed: M1 persistence
///             has no character inventory yet (game.CharacterItems arrives with chantier A3/V2). Page/Index are
///             still bounds-checked like the legacy so a hostile client cannot probe. Wire the 1133
///             check + consumption here once inventory persistence exists.
///         </item>
///         <item>
///             The "tribe leader / in guild / has friends / teacher / student ⇒ 3" refusals: none of those
///             relations exist for login-visible characters yet (guild/friend/teacher tables are A3/V6/V7).
///             Re-add the Result=3 branch when they land.
///         </item>
///     </list>
/// </remarks>
public sealed class ClChangeAvatarNameSendHandler(ICharacterRenameRepository renames)
    : IAsyncPacketHandler<ClChangeAvatarNameSend>
{
    private const int MaxAvatarPost = 2; // MAX_USER_AVATAR_NUM - 1
    private const int InventoryPageCount = 2; // MAX_INVENTORY_PAGE_NUM
    private const int InventorySlotCount = 64; // MAX_INVENTORY_SLOT_NUM
    private const int ResultSqlError = 101; // legacy mDB.ChangeCharacterName "SQL error"

    public async ValueTask HandleAsync(ClChangeAvatarNameSend packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        // Legacy Quit() guards, in source order (S04_MyWork02.cpp l.1310-1348).
        if (packet.AvatarPost is < 0 or > MaxAvatarPost ||
            packet.ChangeAvatarName.Length == 0 ||
            packet.Page is < 0 or >= InventoryPageCount ||
            packet.Index is < 0 or >= InventorySlotCount)
        {
            loginSession.Abort(DisconnectReason.Malformed);
            return;
        }

        int result;
        try
        {
            result = await renames.RenameAsync(accountId, (byte)packet.AvatarPost, packet.ChangeAvatarName,
                cancellationToken);
        }
        catch (Exception)
        {
            // The legacy 101 path: any engine-level failure becomes a client-visible code, never a disconnect.
            result = ResultSqlError;
        }

        session.Send(new LcChangeAvatarNameRecv { Result = result });
    }
}
