using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     GUILD_KICK_CRECV (STRUCT.h:1158-1161, region pack(1)) — CZ_GUILD_WORK_SEND tSort 8's tData layout
///     (contracts/06_guild_tribe.md). Not a packet of its own, same treatment as <see cref="DefaultPData" />.
/// </summary>
[FenrirWireType(13)]
public readonly partial record struct GuildWorkKickPayload : IFenrirWireType<GuildWorkKickPayload>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
