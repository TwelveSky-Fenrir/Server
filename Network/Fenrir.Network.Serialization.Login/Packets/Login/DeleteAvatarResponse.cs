using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.DeleteAvatar, ExpectedSize = 5)]
public readonly partial record struct DeleteAvatarResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
