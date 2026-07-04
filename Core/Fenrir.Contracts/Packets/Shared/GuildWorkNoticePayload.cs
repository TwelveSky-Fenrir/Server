using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     GUILD_NOTICE_INFO_CRECV (STRUCT.h:1167-1170, region pack(1)) — CZ_GUILD_WORK_SEND tSort 5's tData
///     layout (contracts/06_guild_tribe.md). Not a packet of its own, same treatment as <see cref="DefaultPData" />.
/// </summary>
[FenrirWireType(204)]
public readonly partial record struct GuildWorkNoticePayload : IFenrirWireType<GuildWorkNoticePayload>
{
    [FixedArray(4)] [FixedString(51)] public required string[] Notices { get; init; }
}
