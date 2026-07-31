using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(13)]
public readonly partial record struct GuildWorkCreatePayload : IFenrirWireType<GuildWorkCreatePayload>
{
    [FixedString(13)] public required string GuildName { get; init; }
}
