using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar194Status,
    ExpectedSize = 21)]
public readonly partial record struct ZoneWar194StatusResponse : IOutgoingPacket
{
    [FixedArray(4)] public required int[] BattleInfo { get; init; }
    public required int RemainTime { get; init; }
}
