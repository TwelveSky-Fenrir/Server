using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar051Status,
    ExpectedSize = 21)]
public readonly partial record struct ZoneWar051StatusResponse : IOutgoingPacket
{
    [FixedArray(4)] public required int[] ExistStone { get; init; }
    public required int RemainTime { get; init; }
}
