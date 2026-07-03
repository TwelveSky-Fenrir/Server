using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_TRIBE_VOTE_SEND (CLIENT.h:381-385, same struct as CZ_TRIBE_BANK_SEND) — tribe leader election
///     (S04_MyWork02.cpp:11610, <c>TRIBE_VOTE_V2</c> local define). <c>Sort</c> 1 = candidacy at slot
///     <see cref="Value" /> (0-9), 3 = vote for slot <see cref="Value" />. <c>Sort</c> 2 (withdrawal) is
///     dead in EU33 (case compiled out under <c>#ifndef MG5ORIGIN</c>) — default → Quit.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeVoteSend, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzTribeVoteSend : IIncomingPacket<CzTribeVoteSend>
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
