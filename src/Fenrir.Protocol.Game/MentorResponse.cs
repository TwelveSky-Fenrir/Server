using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.Mentor, ExpectedSize = 14)]
public readonly partial record struct MentorResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
