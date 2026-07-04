using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Result: 0 = trade concluded, 1 = trade cancelled.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeEnd, ExpectedSize = 5)]
public readonly partial record struct TradeEndResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
