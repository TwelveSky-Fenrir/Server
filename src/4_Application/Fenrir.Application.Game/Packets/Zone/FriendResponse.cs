using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.Friend, ExpectedSize = 14)]
public readonly partial record struct FriendResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
