using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.WorldSnapshot,
    Compressed = true, ExpectedSize = 3841)]
public readonly partial record struct WorldSnapshotResponse : IOutgoingPacket
{
    public required WorldInfo WorldInfo { get; init; }
    public required TribeInfo TribeInfo { get; init; }
}
