using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TradeAnswer, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct TradeAnswerRequest : IIncomingPacket<TradeAnswerRequest>
{
    public required int Answer { get; init; }
}
