using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelAnswer, ExpectedSize = 5)]
public readonly partial record struct DuelAnswerResponse : IOutgoingPacket
{
    public required int Answer { get; init; }
}
