using Fenrir.Core.Attributes;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.CreateAvatar,
    ExpectedSize = 11173)]
public readonly partial record struct CreateAvatarResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required AvatarInfo AvatarInfo { get; init; }
}
