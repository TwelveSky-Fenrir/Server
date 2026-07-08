using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeAnswer, ExpectedSize = 5)]
public readonly record struct TradeAnswerResponse : IOutgoingPacket
{
    public required int Answer { get; init; }
}
