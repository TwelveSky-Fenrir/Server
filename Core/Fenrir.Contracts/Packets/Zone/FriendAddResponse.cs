using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FriendAdd, ExpectedSize = 18)]
public readonly partial record struct FriendAddResponse : IOutgoingPacket
{
    public required int Index { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
}
