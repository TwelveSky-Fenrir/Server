using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GUILD_WORK_RECV (ZONE.h:842-847, pack(1); builder <c>B_GUILD_WORK_RECV</c> :1044-1050, a raw
///     <c>memcpy</c> of <see cref="GuildInfo" /> incl. its natural-alignment padding) — the sole,
///     always-unicast response to CZ_GUILD_WORK_SEND. <see cref="Sort" /> echoes the requested tSort;
///     legacy leaves <c>mRecv_GuildInfo</c> as uninitialized stack garbage on failure responses (and on
///     the tSort 1001 success) — Fenrir must send a zero-filled <see cref="GuildInfo" /> in those cases.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GuildAction, ExpectedSize = 1397)]
public readonly partial record struct GuildActionResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    public required GuildInfo GuildInfo { get; init; }
}
