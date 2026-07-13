using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.PartyAnswer, ExpectedSize = 5)]
public readonly partial record struct PartyAnswerResponse : IOutgoingPacket
{
    public required int Answer { get; init; }
}
