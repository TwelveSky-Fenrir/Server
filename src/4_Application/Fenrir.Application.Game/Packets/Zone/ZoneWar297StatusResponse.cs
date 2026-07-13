using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar297Status,
    ExpectedSize = 13)]
public readonly partial record struct ZoneWar297StatusResponse : IOutgoingPacket
{
    public required int Value00 { get; init; }
    public required int Value01 { get; init; }
    public required int Value02 { get; init; }
}
