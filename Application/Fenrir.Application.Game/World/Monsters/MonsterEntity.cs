using Fenrir.Data.World;

namespace Fenrir.Application.Game.World.Monsters;

/// <summary>
///     A monster's in-memory, authoritative state while alive in a <see cref="Zone" /> -- the monster twin of
///     <see cref="PlayerRuntimeState" />. Position/AI/aggro fields are mutated ONLY by that zone's own tick
///     (<see cref="MonsterAiSystem" />, <see cref="MonsterSpawnScheduler" /> -- single-writer invariant,
///     architecture reference §10.1), exactly like every field on <see cref="PlayerRuntimeState" />.
/// </summary>
/// <remarks>
///     <see cref="Life" /> is the ONE exception: <see cref="TakeDamage" /> is deliberately safe to call from
///     ANY thread (via <see cref="Zone.TryDamageMonster" />), because the intended caller is a future combat
///     packet handler running on its own session thread, not the zone's tick -- mirroring the SAME narrow,
///     already-established exception <see cref="Zone.ApplyDeath" /> makes for player death. Every OTHER
///     member here (position, AI state, aggro) stays tick-owned; <see cref="TakeDamage" /> touches nothing
///     but the interlocked <c>_life</c>/<c>_deathClaimed</c> backing fields, so calling it concurrently with
///     the zone's own tick can never tear or corrupt any of those tick-owned fields.
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

    /// <summary>The spawn anchor (report 05 §3: <c>mFirstLocation</c>) -- <see cref="MonsterAiState.ReturnToSpawn" />'s destination and the leash origin.</summary>
    public required float HomeX { get; init; }

    public required float HomeY { get; init; }
    public required float HomeZ { get; init; }

    /// <summary>The spawn region's own scatter radius, reused as this monster's leash bound (see <see cref="MonsterAiSystem" />'s remarks for why).</summary>
    public required float LeashRadius { get; init; }

    public int MaxLife { get; init; }

    public MonsterAiState AiState { get; set; } = MonsterAiState.Spawning;

    /// <summary>Legacy ticks spent in the CURRENT <see cref="AiState" /> -- compared against the template's own <c>mFrameInfo</c> thresholds.</summary>
    public int StateTicks { get; set; }

    /// <summary>The currently-locked pursuit target, or null when idle/patrolling/returning.</summary>
    public int? TargetCharacterId { get; set; }

    /// <summary>
    ///     Bounded FIFO aggro list (report 05 §3: <c>MAX_MONSTER_OBJECT_ATTACK_NUM = 50</c>), populated by
    ///     this monster's OWN proximity detection (<see cref="MonsterAiSystem" />). Simplified vs. the legacy's
    ///     parallel per-entry damage/length arrays -- see this task's StructuredOutput openIssues: kill/loot
    ///     attribution in this pass is the explicit attacker id a caller of <see cref="TakeDamage" /> supplies,
    ///     not derived from this list.
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
    ///     Applies damage, clamped to never go below 0. Thread-safe (see class remarks): callable directly
    ///     from a combat packet handler's own async thread, concurrently with this monster's zone tick.
    /// </summary>
    /// <param name="amount">Negative/zero contributes no damage (defensive -- a malformed caller must never HEAL a monster through this path).</param>
    /// <param name="remainingLife">The life value immediately after this call (may already be stale by the time the caller reads it under concurrent damage, same benign race <see cref="PlayerRuntimeState" /> accepts elsewhere).</param>
    /// <returns>
    ///     True ONLY for the single caller whose damage brought this monster to exactly 0 for the first time --
    ///     every other caller (including one that also reduces an already-dead monster's life, a no-op) gets
    ///     false, so death-triggered work (loot roll, respawn scheduling, XP grant) can never run twice for the
    ///     same monster even under concurrent attackers.
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
