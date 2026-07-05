using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar051Status,
    ExpectedSize = 21)]
public readonly partial record struct ZoneWar051StatusResponse : IOutgoingPacket
{
    [FixedArray(4)] public required int[] ExistStone { get; init; }
    public required int RemainTime { get; init; }
}
