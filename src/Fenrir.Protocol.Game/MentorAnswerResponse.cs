using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MentorAnswer, ExpectedSize = 5)]
public readonly partial record struct MentorAnswerResponse : IOutgoingPacket
{
    public required int Answer { get; init; }
}
