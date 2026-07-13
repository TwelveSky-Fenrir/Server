using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Login.Packets;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.DeleteAvatar, ExpectedSize = 5)]
public readonly partial record struct DeleteAvatarResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
