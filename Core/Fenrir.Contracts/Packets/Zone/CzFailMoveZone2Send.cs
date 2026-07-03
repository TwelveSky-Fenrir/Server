using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_FAIL_MOVE_ZONE_2_SEND (CLIENT.h:155-161, header-only, 0-byte payload) — cancels a failed zone transfer;
///     the handler resets <c>mMoveZoneResult = 0</c> and re-registers the user with ts25playuser
///     (<c>U_ZONE_REGISTER_USER_FOR_PLAYUSER_2_SEND</c>), <c>Quit()</c>-ing on re-registration failure. The C++
///     struct is a typedef SHARED with 26 other empty packets (CZ_TEACHER_STATE_SEND, CZ_DUEL_CANCEL_SEND, ...) —
///     modeled here as a dedicated empty record struct per opcode. Extra guard beyond
///     <see cref="ZoneSessionState.InWorld" />: the legacy dispatcher (S04_MyWork01.cpp:312-368) only accepts this
///     opcode mid zone-change when the "moving zone" flag is set (<c>Quit()</c> otherwise); outside a zone change
///     other opcodes are silently ignored while this one is the sole exception — to implement as a "moving zone"
///     flag check in the handler, not as a dedicated session state.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.FailMoveZone2Send, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzFailMoveZone2Send : IIncomingPacket<CzFailMoveZone2Send>
{
}
