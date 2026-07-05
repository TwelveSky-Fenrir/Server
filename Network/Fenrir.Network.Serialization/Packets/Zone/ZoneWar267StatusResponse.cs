using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar267Status,
    ExpectedSize = 21)]
public readonly partial record struct ZoneWar267StatusResponse : IOutgoingPacket
{
    [FixedArray(4)] public required int[] BattleInfo { get; init; }
    public required int RemainTime { get; init; }
}
