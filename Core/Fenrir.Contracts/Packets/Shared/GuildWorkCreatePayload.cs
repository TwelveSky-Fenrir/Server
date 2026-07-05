using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

[FenrirWireType(13)]
public readonly partial record struct GuildWorkCreatePayload : IFenrirWireType<GuildWorkCreatePayload>
{
    [FixedString(13)] public required string GuildName { get; init; }
}
