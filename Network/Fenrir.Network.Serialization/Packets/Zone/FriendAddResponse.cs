using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FriendAdd, ExpectedSize = 18)]
public readonly record struct FriendAddResponse : IOutgoingPacket
{
    public required int Index { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
}
