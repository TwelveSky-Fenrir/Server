using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.CreateAvatarRecv,
    ExpectedSize = 11173)]
public readonly partial record struct LcCreateAvatarRecv : IOutgoingPacket
{
    public required int Result { get; init; }
    public required AvatarInfo AvatarInfo { get; init; }
}
