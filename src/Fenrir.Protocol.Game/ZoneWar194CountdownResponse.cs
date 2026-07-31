using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar194Countdown,
    ExpectedSize = 5)]
public readonly partial record struct ZoneWar194CountdownResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
