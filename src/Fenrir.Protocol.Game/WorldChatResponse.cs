using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.WorldChat, ExpectedSize = 79)]
public readonly partial record struct WorldChatResponse : IOutgoingPacket
{
    public required int TribeRole { get; init; }

    [FixedString(13)] public required string AvatarName { get; init; }
    [FixedString(61)] public required string Content { get; init; }
}
