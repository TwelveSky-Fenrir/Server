using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.DeleteAvatar, ExpectedSize = 5)]
public readonly record struct DeleteAvatarResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
