using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_ANIMAL_STATE_SEND (CLIENT.h:381-385, typedef shared with CZ_TRIBE_BANK_SEND/CZ_TRIBE_VOTE_SEND/
///     CZ_COSTUME_STATE_SEND (90)/CZ_STELLAR_STATE_SEND (153)/CZ_BOTTLE_STATE_SEND (129)) — mount slot
///     management (S04_MyWork02.cpp:11804, body under <c>USE_ANIMAL</c>, active). <c>Sort</c> 1=select
///     (<see cref="Value" />=slot 0..9, MAX_AVATAR_ANIMAL_NUM=10), 2=deselect, 3=mount, 4=dismount,
///     5=delete (<see cref="Value" />=item index, costs 250 CP), 6=convert attribute (requires max exp),
///     7=remove attribute (<see cref="Value" />=1..8, consumes item 1225), 8=transfer attribute
///     (<see cref="Value" />=1..8, consumes item 8425 or 1226). Unknown <c>Sort</c> → Quit. Case 120
///     (tier-2→3 upgrade) is <c>#ifndef LNW33</c>, not compiled in EU33 (falls to default → Quit).
///     Answered by ZC 102 (<c>ZC_ANIMAL_STATE_RECV</c>) unicast; cases 3/4 also broadcast
///     <c>AVATAR_CHANGE_INFO</c> (outside this lot).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.AnimalStateSend, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzAnimalStateSend : IIncomingPacket<CzAnimalStateSend>
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
