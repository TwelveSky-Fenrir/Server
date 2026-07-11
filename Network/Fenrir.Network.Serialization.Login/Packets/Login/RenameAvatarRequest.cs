using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.RenameAvatar,
    ExpectedSize = 34,
    AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly partial record struct RenameAvatarRequest : IIncomingPacket<RenameAvatarRequest>
{
    public required int AvatarPost { get; init; }

    [FixedString(13)] public required string ChangeAvatarName { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }
}
