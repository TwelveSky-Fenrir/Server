using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

[FenrirWireType(13)]
public readonly partial record struct GuildWorkKickPayload : IFenrirWireType<GuildWorkKickPayload>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
