using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.Combat;

/// <summary>
///     Posted by <c>AttackHandler</c> (CZ_PROCESS_ATTACK_SEND, opcode 18) straight from the raw wire request --
///     UNVALIDATED, exactly like <c>ZoneCommand.Move</c> carries a raw <see cref="ActionInfo" />. All guard
///     checks, RNG rolls, and HP mutation happen inside <c>Zone.ApplyCombatCommand</c> on the zone's own tick
///     thread (single-writer invariant, architecture reference §10.1) -- unlike
///     <see cref="Fenrir.Application.Game.Inventory.InventoryZoneCommand" />, this is NOT a pre-decided,
///     already-durable result: combat is zero-SQL, purely in-memory, so (like movement) it belongs entirely on
///     the tick thread rather than split between an async handler and a mirror-only drain.
///     <para>
///         A SEPARATE channel/drain from <see cref="Fenrir.Application.Game.World.ZoneCommand" />'s own
///         <c>DrainInbox</c> switch on purpose: this task (V3 Combat) runs alongside a parallel monster-entity
///         workstream (V4) that may also need to extend <c>Zone</c> -- keeping combat's wiring purely additive
///         (own channel, own drain method, same posture
///         <see cref="Fenrir.Application.Game.Inventory.InventoryZoneCommand" />
///         already established for Inventory) avoids both workstreams needing to touch the same switch statement.
///     </para>
/// </summary>
public readonly struct CombatCommand
{
    public required int AttackerCharacterId { get; init; }
    public required AttackForProtocol AttackInfo { get; init; }
}
