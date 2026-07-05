using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

[FenrirWireType(18)]
public readonly partial record struct GuildWorkTitlePayload : IFenrirWireType<GuildWorkTitlePayload>
{
    [FixedString(13)] public required string AvatarName { get; init; }

    [FixedString(5)] public required string CallName { get; init; }
}
