using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_PARTY_EXILE_RECV (ZONE.h:805-808). Builder is (confusingly) named <c>B_PARTY_KICK_RECV</c> but writes opcode
///     PARTY_EXILE_RECV=78 — the name is a legacy artifact, the wire opcode is correct.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.PartyKick, ExpectedSize = 14)]
public readonly partial record struct PartyKickResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
