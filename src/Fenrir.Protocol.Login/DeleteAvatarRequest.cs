using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.DeleteAvatar, ExpectedSize = 21,
    AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly partial record struct DeleteAvatarRequest : IIncomingPacket<DeleteAvatarRequest>
{
    public required int AvatarPost { get; init; }
    public required int Unknow1 { get; init; }
    public required int Unknow2 { get; init; }
}
