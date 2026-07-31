using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(18)]
public readonly partial record struct GuildWorkTitlePayload : IFenrirWireType<GuildWorkTitlePayload>
{
    [FixedString(13)] public required string AvatarName { get; init; }

    [FixedString(5)] public required string CallName { get; init; }
}
