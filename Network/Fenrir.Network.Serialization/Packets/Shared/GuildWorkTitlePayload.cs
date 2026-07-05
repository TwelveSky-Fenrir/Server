using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(18)]
public readonly partial record struct GuildWorkTitlePayload : IFenrirWireType<GuildWorkTitlePayload>
{
    [FixedString(13)] public required string AvatarName { get; init; }

    [FixedString(5)] public required string CallName { get; init; }
}
