using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar319Phase,
    ExpectedSize = 9)]
public readonly partial record struct ZoneWar319PhaseResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int ZoneNumber { get; init; }
}
