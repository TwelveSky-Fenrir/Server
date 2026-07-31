using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.PartyAnswer, ExpectedSize = 5)]
public readonly partial record struct PartyAnswerResponse : IOutgoingPacket
{
    public required int Answer { get; init; }
}
