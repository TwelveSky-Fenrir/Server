using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.Combat;

/// <summary>
///     Raw, unvalidated wire request -- all guard checks, RNG rolls, and HP mutation happen in
///     <c>Zone.ApplyCombatCommand</c> on the zone's own tick thread.
/// </summary>
public readonly struct CombatCommand
{
    public required int AttackerCharacterId { get; init; }
    public required AttackForProtocol AttackInfo { get; init; }
}
