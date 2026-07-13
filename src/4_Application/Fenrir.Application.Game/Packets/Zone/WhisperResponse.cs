using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.Whisper, ExpectedSize = 111)]
public readonly partial record struct WhisperResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int ZoneNumber { get; init; }

    [FixedString(13)] public required string AvatarName { get; init; }

    [FixedString(61)] public required string Content { get; init; }

    public required int AuthType { get; init; }

    public required ItemLinkInfo Link { get; init; }
}
