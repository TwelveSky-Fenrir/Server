using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

/// <summary>
///     CL_CHANGE_AVATAR_NAME_SEND (CLIENT.h l.88-94): rename the character in <see cref="AvatarPost" /> by
///     consuming a rename scroll (item 1133) at inventory (<see cref="Page" />, <see cref="Index" />)
///     (login protocol report §4.19). Same gate as ops 17/18/22: reachable only once the second-login gate is
///     open — the legacy handler Quit()s on <c>!IsSecondLogin()</c>.
/// </summary>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.RenameAvatar,
    ExpectedSize = 34,
    AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly partial record struct RenameAvatarRequest : IIncomingPacket<RenameAvatarRequest>
{
    public required int AvatarPost { get; init; }

    /// <summary>MAX_AVATAR_NAME_LENGTH=13 (12 chars + NUL).</summary>
    [FixedString(13)]
    public required string ChangeAvatarName { get; init; }

    /// <summary>Inventory page of the rename scroll (0..1, MAX_INVENTORY_PAGE_NUM).</summary>
    public required int Page { get; init; }

    /// <summary>Inventory slot of the rename scroll (0..63, MAX_INVENTORY_SLOT_NUM).</summary>
    public required int Index { get; init; }
}
