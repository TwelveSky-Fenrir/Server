using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.CreateAvatar,
    ExpectedSize = 11173)]
public readonly partial record struct CreateAvatarResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required AvatarInfo AvatarInfo { get; init; }
}
