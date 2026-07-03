using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GUILD_FIND_RECV (ZONE.h:864-867, builder :1069) — unicast response to CZ_GUILD_FIND_SEND;
///     <see cref="Result" /> is the zone number where the searched avatar was found (playuser
///     <c>mRecv_ZoneNumber</c>).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FindGuildMember, ExpectedSize = 5)]
public readonly partial record struct FindGuildMemberResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
