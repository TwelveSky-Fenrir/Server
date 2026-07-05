using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.DeleteAvatar, ExpectedSize = 21,
    AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly partial record struct DeleteAvatarRequest : IIncomingPacket<DeleteAvatarRequest>
{
    public required int AvatarPost { get; init; }
    public required int Unknow1 { get; init; }
    public required int Unknow2 { get; init; }
}
