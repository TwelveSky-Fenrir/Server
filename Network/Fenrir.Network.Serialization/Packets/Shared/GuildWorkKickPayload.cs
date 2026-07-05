using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(13)]
public readonly partial record struct GuildWorkKickPayload : IFenrirWireType<GuildWorkKickPayload>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
