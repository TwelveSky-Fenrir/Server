using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     GUILD_EFFECT_CRECV (S04_MyWork02.cpp:10569-10571, local struct, ambient pack) — CZ_GUILD_WORK_SEND
///     tSort 14's tData layout (USE_GUILD_BUFF, contracts/06_guild_tribe.md). Not a packet of its own, same
///     treatment as <see cref="DefaultPData" />.
/// </summary>
[FenrirWireType(4)]
public readonly partial record struct GuildWorkBuffPayload : IFenrirWireType<GuildWorkBuffPayload>
{
    /// <summary>0-4 (validity checked by the caller -- out of range aborts, matching the legacy's own <c>Quit()</c>).</summary>
    public required int GuildBuffType { get; init; }
}
