using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     GUILD_LOGO_CRECV (STRUCT.h:1214-1217, region pack(1)) — CZ_GUILD_WORK_SEND tSort 1001's tData
///     layout (USE_GUILD_LOGO, contracts/06_guild_tribe.md). Not a packet of its own, same treatment as
///     <see cref="DefaultPData" />.
/// </summary>
[FenrirWireType(4)]
public readonly partial record struct GuildWorkLogoPayload : IFenrirWireType<GuildWorkLogoPayload>
{
    public required int Value { get; init; }
}
