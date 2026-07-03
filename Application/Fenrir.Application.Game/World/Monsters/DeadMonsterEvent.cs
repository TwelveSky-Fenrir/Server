namespace Fenrir.Application.Game.World.Monsters;

/// <summary>
///     Queued by <see cref="Zone.TryDamageMonster" /> (any thread) the instant a monster's life reaches 0,
///     drained by <see cref="MonsterSpawnScheduler" /> on the zone's own next tick (single-writer invariant:
///     loot rolling, ground-item spawning, respawn-timer arming and the XP-grant seam all happen from the
///     tick thread only). Carries the dying <see cref="MonsterEntity" /> itself (already removed from the
///     zone's live monster pool by the time this is enqueued) rather than re-looking it up, since the pool
///     entry is gone by construction.
/// </summary>
public sealed record DeadMonsterEvent(MonsterEntity Monster, int? KillerCharacterId);
