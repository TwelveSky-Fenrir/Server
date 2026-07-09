using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(13)]
public readonly record struct GuildWorkCreatePayload : IFenrirWireType<GuildWorkCreatePayload>
{
    [FixedString(13)] public required string GuildName { get; init; }
}
