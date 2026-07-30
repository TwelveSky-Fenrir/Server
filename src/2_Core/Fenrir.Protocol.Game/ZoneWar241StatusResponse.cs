using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar241Status,
    ExpectedSize = 5)]
public readonly partial record struct ZoneWar241StatusResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
