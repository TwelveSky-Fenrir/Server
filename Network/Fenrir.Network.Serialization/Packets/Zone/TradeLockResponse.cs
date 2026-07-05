using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeLock, ExpectedSize = 5)]
public readonly partial record struct TradeLockResponse : IOutgoingPacket
{
    public required int CheckMe { get; init; }
}
