using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

// Result: 0=renamed, 2=name unchanged/taken, 3=tribe leader/in guild/has friends/teacher/student, 101=SQL error, 102=update failure.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.RenameAvatar,
    ExpectedSize = 5)]
public readonly record struct RenameAvatarResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
