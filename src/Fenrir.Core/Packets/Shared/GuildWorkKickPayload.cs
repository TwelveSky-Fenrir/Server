using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(13)]
public readonly partial record struct GuildWorkKickPayload : IFenrirWireType<GuildWorkKickPayload>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
