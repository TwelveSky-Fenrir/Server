using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(204)]
public readonly record struct GuildWorkNoticePayload : IFenrirWireType<GuildWorkNoticePayload>
{
    [FixedArray(4)] [FixedString(51)] public required string[] Notices { get; init; }
}
