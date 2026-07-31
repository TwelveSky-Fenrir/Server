using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar270Status,
    ExpectedSize = 9)]
public readonly partial record struct ZoneWar270StatusResponse : IOutgoingPacket
{
    public required int Sort { get; init; }

    public required int RemainTime { get; init; }
}
