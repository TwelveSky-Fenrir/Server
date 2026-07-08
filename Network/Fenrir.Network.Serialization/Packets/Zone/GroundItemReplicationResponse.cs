using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>The 2 padding bytes inside ObjectForItem (offsets 62-63) are on the wire, covered by [Reserved(2)].</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GroundItemReplication,
    ExpectedSize = 97)]
public readonly partial record struct GroundItemReplicationResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required ObjectForItem Data { get; init; }
    public required int CheckChangeActionState { get; init; }
}
