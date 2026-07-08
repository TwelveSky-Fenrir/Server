using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

// 17 bytes, no padding before the trailing int despite lying outside a byte boundary.
[FenrirWireType(17)]
public readonly partial record struct GuildWorkAgmPayload : IFenrirWireType<GuildWorkAgmPayload>
{
    [FixedString(13)] public required string AvatarName { get; init; }

    // 1 = promote to sub-master, 2 = demote to member.
    public required int GuildRole { get; init; }
}
