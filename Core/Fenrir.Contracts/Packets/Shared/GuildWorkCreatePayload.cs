using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     CREATE_GUILD_CRECV (STRUCT.h:1154-1157, region pack(1) 1016-1325) — CZ_GUILD_WORK_SEND tSort 1's
///     tData layout (contracts/06_guild_tribe.md §"Calques client→zone"). Not a packet of its own, same
///     treatment as <see cref="DefaultPData" />: the re-read layer <c>GuildActionHandler</c> applies over
///     <c>GuildActionRequest.Data</c>'s first 13 bytes.
/// </summary>
[FenrirWireType(13)]
public readonly partial record struct GuildWorkCreatePayload : IFenrirWireType<GuildWorkCreatePayload>
{
    [FixedString(13)] public required string GuildName { get; init; }
}
