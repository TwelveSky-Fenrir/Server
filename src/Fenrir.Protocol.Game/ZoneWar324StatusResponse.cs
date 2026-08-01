using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar324Status,
    ExpectedSize = 9)]
public readonly partial record struct ZoneWar324StatusResponse : IOutgoingPacket
{
    public required int Sort { get; init; }

    public required int Result { get; init; }
}
