using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FriendAdd, ExpectedSize = 18)]
public readonly record struct FriendAddResponse : IOutgoingPacket
{
    public required int Index { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
}
