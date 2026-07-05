using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeLock, ExpectedSize = 5)]
public readonly partial record struct TradeLockResponse : IOutgoingPacket
{
    public required int CheckMe { get; init; }
}
