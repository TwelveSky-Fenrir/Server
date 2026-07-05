using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MentorAnswer, ExpectedSize = 5)]
public readonly partial record struct MentorAnswerResponse : IOutgoingPacket
{
    public required int Answer { get; init; }
}
