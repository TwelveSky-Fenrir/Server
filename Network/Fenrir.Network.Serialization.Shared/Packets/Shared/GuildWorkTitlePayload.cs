using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(18)]
public readonly partial record struct GuildWorkTitlePayload : IFenrirWireType<GuildWorkTitlePayload>
{
    [FixedString(13)] public required string AvatarName { get; init; }

    [FixedString(5)] public required string CallName { get; init; }
}
