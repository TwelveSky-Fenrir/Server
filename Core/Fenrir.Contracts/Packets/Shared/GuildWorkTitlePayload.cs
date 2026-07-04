using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     GUILD_MAKE_TITLE_CRECV (STRUCT.h:1187-1191, region pack(1)) — CZ_GUILD_WORK_SEND tSort 10's tData
///     layout (contracts/06_guild_tribe.md). Not a packet of its own, same treatment as <see cref="DefaultPData" />.
/// </summary>
[FenrirWireType(18)]
public readonly partial record struct GuildWorkTitlePayload : IFenrirWireType<GuildWorkTitlePayload>
{
    [FixedString(13)] public required string AvatarName { get; init; }

    [FixedString(5)] public required string CallName { get; init; }
}
