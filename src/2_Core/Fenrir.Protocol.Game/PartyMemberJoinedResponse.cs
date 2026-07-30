using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.PartyMemberJoined, ExpectedSize = 14)]
public readonly partial record struct PartyMemberJoinedResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
