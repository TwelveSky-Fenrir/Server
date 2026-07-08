using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>RemainTime = 60 - tick/2, computed at tick 1 then every 10 ticks.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar194Countdown,
    ExpectedSize = 5)]
public readonly partial record struct ZoneWar194CountdownResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
