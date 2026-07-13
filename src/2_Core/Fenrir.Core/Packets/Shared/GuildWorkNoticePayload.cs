using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(204)]
public readonly partial record struct GuildWorkNoticePayload : IFenrirWireType<GuildWorkNoticePayload>
{
    [FixedArray(4)] [FixedString(51)] public required string[] Notices { get; init; }
}
