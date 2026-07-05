using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FriendAnswer, ExpectedSize = 5)]
public readonly partial record struct FriendAnswerResponse : IOutgoingPacket
{
    public required int Answer { get; init; }
}
