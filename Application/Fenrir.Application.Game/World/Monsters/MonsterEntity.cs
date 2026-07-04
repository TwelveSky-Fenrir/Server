using Fenrir.Data.World;

namespace Fenrir.Application.Game.World.Monsters;

/// <summary>
///     A monster's in-memory, authoritative state while alive in a <see cref="Zone" /> -- the monster twin of
///     <see cref="PlayerRuntimeState" />. Mutated only by that zone's own tick (single-writer invariant).
/// </summary>
/// <remarks>
///     <see cref="Life" /> is the one exception: <see cref="TakeDamage" /> is safe to call from any thread (a
///     future combat handler's own session thread), touching only the interlocked <c>_life</c>/
///     <c>_deathClaimed</c> fields, so it can never tear or corrupt the tick-owned fields.
/// </remarks>
public sealed class MonsterEntity
{
    private int _deathClaimed;
    private int _life;

    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required MonsterRowDto Template { get; init; }

    /// <summary>Back-reference so a death event can tell <see cref="MonsterSpawnScheduler" /> which slot to respawn.</summary>
    public required int SpawnSlotId { get; init; }

    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float Heading { get; set; }

    /// <summary>
    ///     Spawn anchor (legacy <c>mFirstLocation</c>) -- <see cref="MonsterAiState.ReturnToSpawn" />'s destination and
    ///     leash origin.
    /// </summary>
    public required float HomeX { get; init; }

    public required float HomeY { get; init; }
    public required float HomeZ { get; init; }

    /// <summary>
    ///     The spawn region's own scatter radius, reused as this monster's leash bound (see
    ///     <see cref="MonsterAiSystem" />'s remarks for why).
    /// </summary>
    public required float LeashRadius { get; init; }

    public int MaxLife { get; init; }

    public MonsterAiState AiState { get; set; } = MonsterAiState.Spawning;

    /// <summary>
    ///     Legacy ticks spent in the CURRENT <see cref="AiState" /> -- compared against the template's own
    ///     <c>mFrameInfo</c> thresholds.
    /// </summary>
    public int StateTicks { get; set; }

    /// <summary>The currently-locked pursuit target, or null when idle/patrolling/returning.</summary>
    public int? TargetCharacterId { get; set; }

    /// <summary>
    ///     Bounded FIFO aggro list (legacy cap 50), populated by this monster's own proximity detection.
    ///     Simplified vs. the legacy's parallel damage/length arrays: kill/loot attribution here is the
    ///     explicit attacker id <see cref="TakeDamage" />'s caller supplies, not derived from this list.
    /// </summary>
    public List<int> AggroCharacterIds { get; } = [];

    public TimeSpan LastRebroadcastAt { get; set; }

    /// <summary>Current HP -- safe to read from any thread (<see cref="Volatile.Read(ref int)" />).</summary>
    public int Life => Volatile.Read(ref _life);

    public static MonsterEntity Create(int serverIndex, uint uniqueNumber, MonsterRowDto template, int spawnSlotId,
        float homeX, float homeY, float homeZ, float leashRadius)
    {
        var entity = new MonsterEntity
        {
            ServerIndex = serverIndex,
            UniqueNumber = uniqueNumber,
            Template = template,
            SpawnSlotId = spawnSlotId,
            HomeX = homeX,
            HomeY = homeY,
            HomeZ = homeZ,
            LeashRadius = leashRadius,
            MaxLife = template.Life,
            PosX = homeX,
            PosY = homeY,
            PosZ = homeZ
        };
        entity._life = template.Life;
        return entity;
    }

    /// <summary>
    ///     Applies damage, clamped to never go below 0. Thread-safe (see class remarks): callable concurrently
    ///     with this monster's zone tick.
    /// </summary>
    /// <param name="amount">Negative/zero contributes no damage -- a malformed caller must never heal a monster.</param>
    /// <param name="remainingLife">Life immediately after this call; may already be stale under concurrent damage.</param>
    /// <returns>
    ///     True only for the single caller whose damage brought this monster to exactly 0 for the first time,
    ///     so death-triggered work (loot, respawn, XP) can never run twice for the same monster.
    /// </returns>
    public bool TakeDamage(int amount, out int remainingLife)
    {
        if (amount < 0)
            amount = 0;

        int oldLife, newLife;
        do
        {
            oldLife = Volatile.Read(ref _life);
            if (oldLife <= 0)
            {
                remainingLife = 0;
                return false; // already dead -- a duplicate/late hit is a no-op, not a second kill
            }

            newLife = Math.Max(0, oldLife - amount);
        } while (Interlocked.CompareExchange(ref _life, newLife, oldLife) != oldLife);

        remainingLife = newLife;
        return newLife == 0 && Interlocked.CompareExchange(ref _deathClaimed, 1, 0) == 0;
    }
}
