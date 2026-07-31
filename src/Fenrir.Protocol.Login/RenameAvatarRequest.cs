using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Login;

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
