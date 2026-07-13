using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar049Status,
    ExpectedSize = 21)]
public readonly partial record struct ZoneWar049StatusResponse : IOutgoingPacket
{
    [FixedArray(4)] public required int[] TribeUserNum { get; init; }
    public required int RemainTime { get; init; }
}
