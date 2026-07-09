using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelCountdown, ExpectedSize = 5)]
public readonly record struct DuelCountdownResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
