using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_PROCESS_ATTACK_SEND (CLIENT.h:199-202, 68-byte payload = <see cref="AttackForProtocol" /> whole) — the
///     client's attack proposal, handled by <c>W_PROCESS_ATTACK_SEND</c> which switches on <c>mCase</c> to
///     <c>MyGame::ProcessAttack01..06</c>. Any <c>mCase</c> outside 1..6 must <c>Quit()</c> the session
///     (anti-fuzzing behaviour to replicate in the handler). The server must NOT trust the client-filled result
///     fields: it fully recomputes them before echoing ZC_PROCESS_ATTACK_RECV. The UNITY re-marshalling block
///     (S04_MyWork02.cpp:1947-1974) is dead code in this build (UNITY off).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ProcessAttackSend, ExpectedSize = 77,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzProcessAttackSend : IIncomingPacket<CzProcessAttackSend>
{
    public required AttackForProtocol AttackInfo { get; init; }
}
