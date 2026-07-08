using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>
///     CZ_CHANGE_TO_TRIBE4_SEND, opcode 37 -- request to convert to (or return from) the 4th, neutral
///     "Fujin" tribe. Carries no payload fields beyond the standard header: the character undergoing
///     conversion is resolved entirely from the authenticated session, never from anything client-supplied.
///     Pairs with <see cref="TribeMigrationResponse" /> (ZC_CHANGE_TO_TRIBE4_RECV, opcode 40).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/Protocol/CLIENT.h:155-161 (empty request struct, not independently
///     re-verified this task -- carried forward from the behavior contract's input research).
///     Server/ts25zone/S04_MyWork02.cpp:7565-7758 is the full legacy handler this packet's own handler
///     (<see cref="Fenrir.Application.Game.Handlers.Handlers.Tribes.TribeMigrationHandler" />, delegating to
///     <c>Fenrir.Application.Game.Services.Tribes.TribeMigrationService</c> and
///     <c>Fenrir.Application.Game.Domain.Tribes.TribeMigrationGate</c>) implements gating/side effects
///     against, per the <c>legacy-behavior-translator</c> contract for "Fourth-tribe (Fujin) conversion and
///     return" -- that legacy handler unconditionally disconnected the client as its first statement in
///     every shipped build (S04_MyWork02.cpp:7567-7568), so this opcode was never reachable in the legacy
///     game; it is reachable for the first time here.
/// </remarks>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeMigration,
    ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribeMigrationRequest : IIncomingPacket<TribeMigrationRequest>
{
}
