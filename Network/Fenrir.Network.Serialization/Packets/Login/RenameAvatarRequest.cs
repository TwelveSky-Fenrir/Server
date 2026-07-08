using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

// Consumes a rename scroll (item 1133) at inventory (Page, Index); requires the second-login gate open.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.RenameAvatar,
    ExpectedSize = 34,
    AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly partial record struct RenameAvatarRequest : IIncomingPacket<RenameAvatarRequest>
{
    public required int AvatarPost { get; init; }

    [FixedString(13)] public required string ChangeAvatarName { get; init; }

    // 0..1 (MAX_INVENTORY_PAGE_NUM).
    public required int Page { get; init; }

    // 0..63 (MAX_INVENTORY_SLOT_NUM).
    public required int Index { get; init; }
}
