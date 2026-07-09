using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.WorldSnapshot,
    Compressed = true, ExpectedSize = 3841)]
public readonly record struct WorldSnapshotResponse : IOutgoingPacket
{
    public required WorldInfo WorldInfo { get; init; }
    public required TribeInfo TribeInfo { get; init; }
}
