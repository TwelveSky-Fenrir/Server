using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AutoBuffRegister,
    ExpectedSize = 5)]
public readonly partial record struct AutoBuffRegisterResponse : IOutgoingPacket
{
    public required int Value { get; init; }
}
