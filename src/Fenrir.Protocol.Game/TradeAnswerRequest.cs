using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TradeAnswer, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct TradeAnswerRequest : IIncomingPacket<TradeAnswerRequest>
{
    public required int Answer { get; init; }
}
