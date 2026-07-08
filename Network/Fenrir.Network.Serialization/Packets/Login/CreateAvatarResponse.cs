using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.CreateAvatar,
    ExpectedSize = 11173)]
public readonly record struct CreateAvatarResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required AvatarInfo AvatarInfo { get; init; }
}
