using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     GUILD_MAKE_AGM_CRECV (STRUCT.h:1182-1186, region pack(1) 1016-1325 -- 17 bytes, NO padding before
///     the trailing int despite lying outside a byte boundary; contracts/06_guild_tribe.md corrects doc
///     10's "pad interne" claim). CZ_GUILD_WORK_SEND tSort 9's tData layout. Not a packet of its own, same
///     treatment as <see cref="DefaultPData" />.
/// </summary>
[FenrirWireType(17)]
public readonly partial record struct GuildWorkAgmPayload : IFenrirWireType<GuildWorkAgmPayload>
{
    [FixedString(13)] public required string AvatarName { get; init; }

    /// <summary>1 = promote to sub-master, 2 = demote to member.</summary>
    public required int GuildRole { get; init; }
}
