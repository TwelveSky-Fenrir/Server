using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar335Countdown,
    ExpectedSize = 5)]
public readonly partial record struct ZoneWar335CountdownResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
