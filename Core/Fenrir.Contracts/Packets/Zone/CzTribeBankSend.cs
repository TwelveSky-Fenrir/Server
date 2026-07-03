using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_TRIBE_BANK_SEND (CLIENT.h:381-385, struct shared with CZ_TRIBE_VOTE_SEND/ANIMAL_STATE/etc.) —
///     tribe bank operations (S04_MyWork02.cpp:11560). <c>Sort</c> 1 = view (RPC playuser
///     <c>TRIBE_BANK_VIEW</c>), 2 = withdraw one slot (<see cref="Value" /> = slot index 0-49, RPC
///     playuser <c>TRIBE_BANK_LOAD</c>). Answered with ZC_TRIBE_BANK_RECV, except on withdraw failure
///     (silent Quit).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeBankSend, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzTribeBankSend : IIncomingPacket<CzTribeBankSend>
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
