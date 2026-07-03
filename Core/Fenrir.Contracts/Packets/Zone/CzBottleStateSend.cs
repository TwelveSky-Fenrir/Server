using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_BOTTLE_STATE_SEND (CLIENT.h:381-385, same typedef as CZ 87) — drink a bottle
///     (S04_MyWork02.cpp:14763). Only <c>Sort == 0</c> is a success, with <see cref="Value" /> = bottle
///     slot 0..9 (<c>wAvatar.aBottle[]</c>); requires stock &gt; 0 AND (no mount OR absorption active).
///     Effect: 120s of drunkenness (<c>aBottleTime</c>), stats recalculated. Any other case (including
///     <c>Sort</c> != 0) → error response, NOT a Quit. Answered by ZC 165 (<c>ZC_DRUNK_INFO_RECV</c>,
///     13 bytes, outside this lot) + <c>AVATAR_CHANGE_INFO_2</c> (S112USE_BOTTLE).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.BottleStateSend, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzBottleStateSend : IIncomingPacket<CzBottleStateSend>
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
