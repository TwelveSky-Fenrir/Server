using Fenrir.Core.Attributes;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.WorldSnapshot,
    Compressed = true, ExpectedSize = 3841)]
public readonly partial record struct WorldSnapshotResponse : IOutgoingPacket
{
    public required WorldInfo WorldInfo { get; init; }
    public required TribeInfo TribeInfo { get; init; }
}
