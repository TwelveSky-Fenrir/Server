using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     GUILD_TRANSFER_LEADER_CRECV (STRUCT.h:1203-1207, region pack(1)) — CZ_GUILD_WORK_SEND tSort 17's
///     tData layout (contracts/06_guild_tribe.md). Not a packet of its own, same treatment as
///     <see cref="DefaultPData" />. <see cref="OldMasterName" /> is read but then OVERWRITTEN by the
///     zone's own <c>wAvatar.aName</c> in the legacy (doc 10 §1 tSort 17) -- carried here for completeness
///     but the caller should use the REQUESTER's own name, never this field, for the "current master" side.
/// </summary>
[FenrirWireType(26)]
public readonly partial record struct GuildWorkTransferPayload : IFenrirWireType<GuildWorkTransferPayload>
{
    [FixedString(13)] public required string NewMasterName { get; init; }

    [FixedString(13)] public required string OldMasterName { get; init; }
}
