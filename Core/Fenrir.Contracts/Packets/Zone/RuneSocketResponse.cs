using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Result: 0 = removal ok, 1 = insertion ok, 2 = inventory full.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.RuneSocket, ExpectedSize = 21)]
public readonly partial record struct RuneSocketResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }

    public required int ItemIndex { get; init; }

    public required int RuneIndex { get; init; }
}
