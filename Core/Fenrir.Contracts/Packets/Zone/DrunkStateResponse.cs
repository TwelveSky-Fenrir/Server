using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DrunkState, ExpectedSize = 13)]
public readonly partial record struct DrunkStateResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Result { get; init; }
    public required int BottleIndex { get; init; }
}
