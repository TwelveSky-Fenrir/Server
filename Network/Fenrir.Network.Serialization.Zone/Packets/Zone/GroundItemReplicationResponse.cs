using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GroundItemReplication,
    ExpectedSize = 97)]
public readonly partial record struct GroundItemReplicationResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required ObjectForItem Data { get; init; }
    public required int CheckChangeActionState { get; init; }
}
