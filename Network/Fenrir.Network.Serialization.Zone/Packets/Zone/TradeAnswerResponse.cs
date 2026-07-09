using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeAnswer, ExpectedSize = 5)]
public readonly record struct TradeAnswerResponse : IOutgoingPacket
{
    public required int Answer { get; init; }
}
