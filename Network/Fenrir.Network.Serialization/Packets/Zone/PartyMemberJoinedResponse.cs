using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.PartyMemberJoined, ExpectedSize = 14)]
public readonly record struct PartyMemberJoinedResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
