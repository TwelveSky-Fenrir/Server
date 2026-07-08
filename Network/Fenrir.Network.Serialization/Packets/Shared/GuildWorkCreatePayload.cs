using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(13)]
public readonly record struct GuildWorkCreatePayload : IFenrirWireType<GuildWorkCreatePayload>
{
    [FixedString(13)] public required string GuildName { get; init; }
}
