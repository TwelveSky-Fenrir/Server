using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.DeleteAvatarRecv, ExpectedSize = 5)]
public readonly partial record struct LcDeleteAvatarRecv : IOutgoingPacket
{
    public required int Result { get; init; }
}
