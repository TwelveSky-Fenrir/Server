using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_TRIBE_VOTE_RECV (ZONE.h:905-910, builder :1116-1122) — unicast response to CZ_TRIBE_VOTE_SEND.
///     Only tSort 1 (candidacy) and 3 (vote) are ever emitted in EU33; tSort 2's case and the non-V2 path
///     of tSort 1 are compiled out.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeVote, ExpectedSize = 13)]
public readonly partial record struct TribeVoteResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
