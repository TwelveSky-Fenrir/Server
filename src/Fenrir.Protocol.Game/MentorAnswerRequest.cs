using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.MentorAnswer, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct MentorAnswerRequest : IIncomingPacket<MentorAnswerRequest>
{
    public required int Answer { get; init; }
}
