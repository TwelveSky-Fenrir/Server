using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(204)]
public readonly record struct GuildWorkNoticePayload : IFenrirWireType<GuildWorkNoticePayload>
{
    [FixedArray(4)] [FixedString(51)] public required string[] Notices { get; init; }
}
