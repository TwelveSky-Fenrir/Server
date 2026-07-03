using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_PROCESS_ATTACK_RECV (ZONE.h:448-451, 68-byte payload = <see cref="AttackForProtocol" /> whole) — result of
///     the server-side damage computation (<c>MyGame::ProcessAttack01..06</c>), AOI broadcast + unicast to the
///     attacker. Same wire shape as CZ_PROCESS_ATTACK_SEND: the server overwrites the result fields
///     (<c>AttackResultValue</c>, <c>AttackCriticalExist</c>, <c>AttackElementDamage</c>,
///     <c>AttackViewDamageValue</c>, <c>AttackRealDamageValue</c>) before rebroadcasting.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ProcessAttackRecv, ExpectedSize = 69)]
public readonly partial record struct ZcProcessAttackRecv : IOutgoingPacket
{
    public required AttackForProtocol AttackInfo { get; init; }
}
