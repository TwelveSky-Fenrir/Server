using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(17)]
public readonly partial record struct GuildWorkAgmPayload : IFenrirWireType<GuildWorkAgmPayload>
{
    [FixedString(13)] public required string AvatarName { get; init; }

    public required int GuildRole { get; init; }
}
