using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// The only forge response without a trailing Value[6] (9 bytes, not 33 like the other forge ZCs).
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CombineItem, ExpectedSize = 9)]
public readonly partial record struct CombineItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Cost { get; init; }
}
