namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     One attacker's tracked hit-history entry against a single <see cref="MonsterEntity" /> -- legacy
///     <c>mAttackDamage[]</c> slot (<c>SetAttackInfoWithAvatar</c>, <c>Server/ts25zone/S07_MyGame05.cpp:1675-1720</c>).
///     Written from two distinct call reasons sharing this same table, matching legacy: a zero-seeded
///     write-through the instant a monster's own AI acquires a new pursuit target
///     (<see cref="MonsterEntity.RegisterAcquisition" />), and an accrual on every landed hit
///     (<see cref="MonsterEntity.RegisterAttackDamage" />) -- so <see cref="CumulativeDamage" /> can be exactly
///     zero for an entry that was only ever acquired, never hit. Consumed only by <see cref="World.Zone" />'s
///     own kill-credit selection (<see cref="World.Zone.TryDamageMonster" />) at the moment a monster dies.
/// </summary>
/// <remarks>
///     <see cref="SessionToken" /> is the attacker's live <see cref="PlayerRuntimeState" /> instance at the
///     moment of the hit -- a fresh object per <c>Zone.HandleEnter</c> for every world entry, reconnect
///     included. It stands in for legacy's own <c>mIndex</c>-keyed session identity (a fixed shared-memory
///     avatar slot a later login could otherwise reuse): once a character leaves and re-enters, its zone
///     holds a brand-new <see cref="PlayerRuntimeState" />, so re-checking this reference at kill-credit
///     selection time naturally rejects a stale, pre-reconnect entry -- the same "stale slot" rejection
///     legacy performs via its own explicit session-identifier check
///     (<c>SelectAvatarIndexForMaxAttackDamage</c>, <c>S07_MyGame05.cpp:1723-1780</c>).
/// </remarks>
internal sealed class MonsterAttackDamageEntry
{
    public required int CharacterId { get; init; }
    public required object SessionToken { get; init; }
    public long CumulativeDamage { get; set; }
}
