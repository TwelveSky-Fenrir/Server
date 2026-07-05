namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     Queued by <see cref="Zone.TryDamageMonster" /> (any thread) on kill, drained by
///     <see cref="MonsterSpawnScheduler" /> on the zone's own tick. Carries the removed
///     <see cref="MonsterEntity" /> directly since it's already gone from the live pool.
/// </summary>
public sealed record DeadMonsterEvent(MonsterEntity Monster, int? KillerCharacterId);
