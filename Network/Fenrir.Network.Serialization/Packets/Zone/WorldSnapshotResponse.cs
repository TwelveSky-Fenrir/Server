using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.WorldSnapshot,
    Compressed = true, ExpectedSize = 3841)]
public readonly partial record struct WorldSnapshotResponse : IOutgoingPacket
{
    public required WorldInfo WorldInfo { get; init; }
    public required TribeInfo TribeInfo { get; init; }
}
