using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_STELLAR_STATE_SEND (CLIENT.h:381-385, same typedef as CZ 87) — stellar core slot management,
///     mirroring CZ 90 for the LNW33-specific stellar core system (<c>USE_STELLAR_CORE</c> active,
///     S04_MyWork02.cpp:15511). <c>Sort</c> 1=select (<see cref="Value" />=slot 0..9,
///     MAX_AVATAR_STELLAR_NUM=10), 2=no-op, 3=equip, 4=remove, 5=return to inventory. Other → Quit.
///     Answered by ZC 193 (<c>ZC_STELLAR_STATE_RECV</c>, 29 bytes — NOT the 8th int of ZC 108); cases 3/4
///     also broadcast <c>AVATAR_CHANGE_INFO_1</c> (sorts 37/38).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.StellarStateSend, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzStellarStateSend : IIncomingPacket<CzStellarStateSend>
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
