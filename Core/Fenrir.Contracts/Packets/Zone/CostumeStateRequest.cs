using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_COSTUME_STATE_SEND (CLIENT.h:381-385, same typedef as CZ 87) — costume slot management
///     (S04_MyWork02.cpp:12663). <c>Sort</c> 1=select (<see cref="Value" />=slot 0..9,
///     MAX_AVATAR_COSTUME_NUM=10), 2=no-op (return), 3=equip the selected costume, 4=remove, 5=return
///     costume <see cref="Value" /> to the inventory (with enchant date under
///     <c>USE_ENCHANT_COSTUME</c> and expiry date if rented). Unknown <c>Sort</c> → Quit. Answered by
///     ZC 108 (<c>ZC_COSTUME_STATE_RECV</c>, 33 bytes) unicast; cases 3/4 also broadcast
///     <c>AVATAR_CHANGE_INFO_1</c> (sorts 16/17) to nearby players.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.CostumeState, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CostumeStateRequest : IIncomingPacket<CostumeStateRequest>
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
