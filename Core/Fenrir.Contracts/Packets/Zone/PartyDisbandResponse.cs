using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_PARTY_BREAK_RECV (ZONE.h:820-824). In EU33 (USE_PARTY_V3 off) <c>Sort</c> is constant <c>1</c>: case 109
///     (leader BREAK) sends <c>Sort=1, AvatarName=""</c> to all members; case 108 (member absent from the PARTY_MAKE_INFO
///     roster) sends <c>Sort=1, AvatarName=&lt;slot 1 name&gt;</c>.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.PartyDisband, ExpectedSize = 18)]
public readonly partial record struct PartyDisbandResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
}
