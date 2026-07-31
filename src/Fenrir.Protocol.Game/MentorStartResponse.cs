using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MentorStart, ExpectedSize = 18)]
public readonly partial record struct MentorStartResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
}
