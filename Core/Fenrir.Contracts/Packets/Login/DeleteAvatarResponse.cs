using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.DeleteAvatar, ExpectedSize = 5)]
public readonly partial record struct DeleteAvatarResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
