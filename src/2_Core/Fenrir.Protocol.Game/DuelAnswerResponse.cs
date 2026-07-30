using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelAnswer, ExpectedSize = 5)]
public readonly partial record struct DuelAnswerResponse : IOutgoingPacket
{
    public required int Answer { get; init; }
}
