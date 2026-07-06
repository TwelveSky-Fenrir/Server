namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     Queued by <see cref="Zone.TryDamageMonster" /> (any thread) on kill, drained by
///     <see cref="MonsterSpawnScheduler" /> on the zone's own tick. Carries the removed
///     <see cref="MonsterEntity" /> directly since it's already gone from the live pool.
/// </summary>
/// <param name="KillerCharacterId">
///     The resolved kill-credit target -- <see cref="Zone.SelectMonsterKillCredit" />'s own output, not
///     necessarily whoever landed the killing blow. Null means no eligible candidate was found (an
///     unattributed kill), which <see cref="MonsterSpawnScheduler.ProcessDeath" /> already treats as "grant
///     nothing," same as legacy's own <c>tSelectAvatarIndex == -1</c> gate.
/// </param>
public sealed record DeadMonsterEvent(MonsterEntity Monster, int? KillerCharacterId);
