using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     A monster's in-memory, authoritative state while alive in a <see cref="Zone" /> -- the monster twin of
///     <see cref="PlayerRuntimeState" />. Mutated only by that zone's own tick (single-writer invariant).
/// </summary>
/// <remarks>
///     <see cref="Life" /> is the one exception: <see cref="TakeDamage" /> is safe to call from any thread (a
///     future combat handler's own session thread), touching only the interlocked <c>_life</c>/
///     <c>_deathClaimed</c> fields, so it can never tear or corrupt the tick-owned fields.
///     <see cref="RegisterAttackDamage" />/<see cref="SnapshotAttackDamage" /> share that same
///     any-thread-safe posture, guarded by their own <see cref="_attackDamageLock" /> instead of an
///     interlocked primitive since a whole entry (not a single scalar) is mutated per call.
/// </remarks>
public sealed class MonsterEntity
{
    /// <summary>Legacy <c>MAX_MONSTER_OBJECT_ATTACK_NUM</c> (<c>Server/Header/Protocol/DEFINE.h:603</c>).</summary>
    private const int MaxAttackDamageEntries = 50;

    private readonly List<MonsterAttackDamageEntry> _attackDamage = [];
    private readonly Lock _attackDamageLock = new();

    private int _deathClaimed;
    private int _life;

    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required MonsterRowDto Template { get; init; }

    /// <summary>Back-reference so a death event can tell <see cref="MonsterSpawnScheduler" /> which slot to respawn.</summary>
    public required int SpawnSlotId { get; init; }

    /// <summary>
    ///     Zone-241 "LOD" personal-dungeon tag (legacy <c>DUNGEON_INSTANCE::mID</c>, threaded onto the monster
    ///     at summon time) -- null for every ordinary world/region monster. Non-null only for a personal boss
    ///     created via <see cref="Zone.SummonPersonalBoss" />, and equal to the owning avatar's own
    ///     <see cref="PlayerRuntimeState.DungeonInstanceId" />. Broadcast/targeting/pickup filter on this field
    ///     rather than maintaining a membership list (legacy's own list-based <c>AddMonster</c> is dead code --
    ///     see <c>Zone.DungeonInstance.cs</c>'s remarks).
    /// </summary>
    public int? InstanceId { get; init; }

    /// <summary>
    ///     Legacy <c>mAvatarName</c> (<c>Server/ts25zone/H07_MyGame.h</c>) -- the name of the avatar this monster
    ///     is summon-locked to, stamped on at summon time. Null/empty for every ordinary monster (the
    ///     overwhelming majority); no Fenrir spawn path sets this today because no player-summon-monster
    ///     mechanic exists yet -- see <see cref="Combat.MonsterCombatResolver.ResolvePvmAttack" />'s
    ///     owner-name-lock check, the sole consumer, for the rejection rule this field feeds.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S07_MyGame02.cpp:1885-1896 (owner-name-lock check, re-verified directly
    ///     from source; unless this field is empty, the attacking avatar's own name must match it, or the
    ///     attack is rejected -- one narrow exception, see <see cref="OwnerNameLockExemptionArmedAt" />).
    /// </remarks>
    public string? OwnerName { get; init; }

    /// <summary>
    ///     Legacy <c>mInvalidTimeForSummon</c> (<c>Server/ts25zone/H07_MyGame.h:1096</c>), reproduced ONLY for
    ///     its single narrow reuse as the elapsed-time gate on the one <see cref="OwnerName" />-lock exemption
    ///     (monster template 9002 in the shipped LNW33 build -- see
    ///     <see cref="Combat.MonsterCombatResolver.ResolvePvmAttack" />). Legacy's <c>mInvalidTimeForSummon</c>
    ///     is also reused, unrelated to the owner-name-lock check, by several other summon/respawn-readiness
    ///     mechanics (<c>Server/ts25zone/S10_MySummon.cpp</c>, <c>Server/ts25zone/ZoneWorker.cpp</c>,
    ///     <c>Server/ts25zone/S07_MyGame02.cpp:3129</c> on death) -- none of those are modeled by this field;
    ///     they belong to a future summon-monster mechanic's own behavior contract. Null until a summon
    ///     mechanic arms it; the owner-name-lock check treats null the same as "not enough time has elapsed
    ///     yet," matching legacy's own zero-elapsed state immediately after spawn
    ///     (<c>Server/ts25zone/S07_MyGame05.cpp:14</c>, <c>MONSTER_OBJECT::Init</c> sets it to the current tick).
    /// </remarks>
    public TimeSpan? OwnerNameLockExemptionArmedAt { get; init; }

    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float Heading { get; set; }

    /// <summary>
    ///     This monster's own current cell in <see cref="Zone" />'s monster-side AOI grid -- the monster
    ///     counterpart of <see cref="PlayerRuntimeState.CurrentCell" />. Tick-owned only; kept in sync by
    ///     <see cref="Zone.SpawnMonster" /> (initial registration) and <see cref="Zone.SyncMonsterCell" />
    ///     (every AI tick that moves this monster) -- see <see cref="Zone.SendExistingMonstersTo" />'s own
    ///     remarks for why this grid exists.
    /// </summary>
    public (int X, int Z) CurrentCell { get; set; }

    /// <summary>
    ///     Spawn anchor (legacy <c>mFirstLocation</c>) -- <see cref="MonsterAiState.ReturnToSpawn" />'s
    ///     destination. NOT the basis of <see cref="MonsterAiSystem.RunChase" />'s chase give-up guard, which
    ///     compares the monster's live position against its TARGET's live position instead (see
    ///     <see cref="LeashRadius" />'s own remarks).
    /// </summary>
    public required float HomeX { get; init; }

    public required float HomeY { get; init; }
    public required float HomeZ { get; init; }

    /// <summary>
    ///     The spawn region's own scatter radius. An earlier revision of <see cref="MonsterAiSystem.RunChase" />
    ///     reused this as a distance-from-home chase-leash bound with no legacy citation for that mechanic;
    ///     legacy's actual chase give-up guard (<c>S07_MyGame05.cpp:1359-1366</c>) instead compares the
    ///     monster's own live position against its TARGET's live position, never against home (see
    ///     <see cref="MonsterAiSystem" />'s remarks). This field is retained only because every spawn call
    ///     site already populates it -- it is no longer read by any give-up/leash check.
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
    ///     Distinct from the separate <see cref="RegisterAttackDamage" />/<see cref="SnapshotAttackDamage" />
    ///     per-attacker damage-history table that actually drives kill/loot credit selection
    ///     (<see cref="Zone.TryDamageMonster" />, <see cref="Zone.SelectMonsterKillCredit" />) -- this list is
    ///     proximity-driven, not damage-driven, and is read by nothing for attribution purposes.
    /// </summary>
    public List<int> AggroCharacterIds { get; } = [];

    public TimeSpan LastRebroadcastAt { get; set; }

    /// <summary>
    ///     Legacy ticks accumulated since the last <c>SelectAvatarIndexForPossibleAttack</c> throttle-check
    ///     attempt -- reset to 0 whenever <see cref="MonsterAiSystem" /> actually runs a detection scan
    ///     (successful or not), matching legacy <c>mCheckDetectEnemyTime</c>'s "restarts on every attempt"
    ///     semantics (<c>S07_MyGame05.cpp:127-131</c>). Starts at 0, so a freshly spawned monster's very first
    ///     scan is still subject to the full throttle window, same as legacy's own zero-initialized timestamp.
    /// </summary>
    public int DetectionThrottleTicks { get; set; }

    /// <summary>
    ///     Legacy ticks accumulated toward the 60-second in-place re-detection grace period (legacy
    ///     <c>mCheckFirstLocationTime</c>, <c>S07_MyGame05.cpp:1012-1031</c>) -- advances only across ticks
    ///     spent in <see cref="MonsterAiState.Decision" /> that fail to (re)acquire a target; unconditionally
    ///     reset to 0 the instant it crosses
    ///     <see cref="Simulation.SimulationClock.MonsterIdleReturnHomeLegacyTicks" />, whether or not that same
    ///     tick actually starts a return-to-spawn (an idle monster already home resets this with no other
    ///     observable effect). NOT reset merely by losing a chase target -- see
    ///     <see cref="MonsterAiSystem.RunChase" />'s remarks: this is a rolling clock, not a fresh countdown
    ///     starting the moment a target is lost.
    /// </summary>
    /// <remarks>
    ///     Starts at 0 for a freshly spawned, non-summon monster: the generic <c>MONSTER_OBJECT::Init</c>
    ///     (<c>S07_MyGame05.cpp:7-26</c>) does not explicitly initialize the legacy timer this field mirrors;
    ///     the only explicit initialization found is summon-specific (<c>S10_MySummon.cpp:797-798</c>). Zero-init
    ///     mirrors this class's existing <see cref="DetectionThrottleTicks" /> convention for the same reason --
    ///     flagged as an open question by the originating behavior contract, not a verified legacy default.
    /// </remarks>
    public int IdleReturnElapsedTicks { get; set; }

    /// <summary>
    ///     Legacy ticks accumulated toward the 40-second idle random-wander fallback (legacy
    ///     <c>mCheckLastWalkTime</c>, <c>S07_MyGame05.cpp:1033-1057</c>) -- same accrual/reset posture and
    ///     zero-init caveat as <see cref="IdleReturnElapsedTicks" />; additionally reset the instant this
    ///     monster actually begins a return-to-spawn (<c>S07_MyGame05.cpp:1024</c>).
    /// </summary>
    public int IdleWanderElapsedTicks { get; set; }

    /// <summary>
    ///     The idle random-wander destination most recently rolled by <see cref="MonsterAiSystem" /> (legacy
    ///     <c>aTargetLocation</c> while <c>aSort == 3</c>) -- only meaningful while <see cref="AiState" /> is
    ///     <see cref="MonsterAiState.Patrol" />, always set immediately before that transition.
    /// </summary>
    public float WanderTargetX { get; set; }

    /// <summary>See <see cref="WanderTargetX" />.</summary>
    public float WanderTargetZ { get; set; }

    /// <summary>
    ///     This instance's own rolled anti-clump pursuer cap (legacy <c>mSameTargetPostNum</c>) -- drawn once at
    ///     spawn, uniformly, from <see cref="MonsterRowDto.FollowInfo1" />/<see cref="MonsterRowDto.FollowInfo2" />
    ///     (<c>S10_MySummon.cpp:795</c>) and fixed for this instance's whole lifetime; a different,
    ///     freshly-spawned instance of the same monster type can roll a different value. Consumed by
    ///     <see cref="MonsterAiSystem" />'s anti-clump filter.
    /// </summary>
    public int PursuerCapacity { get; init; }

    /// <summary>Current HP -- safe to read from any thread (<see cref="Volatile.Read(ref int)" />).</summary>
    public int Life => Volatile.Read(ref _life);

    /// <summary>Builds a freshly spawned instance, seeded at full life and parked at its own home point.</summary>
    /// <param name="random">
    ///     Draws the one-time <see cref="PursuerCapacity" /> roll -- defaults to the shared, thread-safe
    ///     <see cref="SystemRandomSource" /> so every production spawn call site gets a real roll without
    ///     having to thread a random source through; tests that need a deterministic capacity can pass one.
    /// </param>
    public static MonsterEntity Create(int serverIndex, uint uniqueNumber, MonsterRowDto template, int spawnSlotId,
        float homeX, float homeY, float homeZ, float leashRadius, int? instanceId = null,
        IRandomSource? random = null)
    {
        var rng = random ?? SystemRandomSource.Instance;

        // mSameTargetPostNum (S10_MySummon.cpp:795): uniform roll over [FollowInfo1, FollowInfo2] inclusive,
        // once per spawned instance -- same "max not greater than min collapses to min" convention as
        // MonsterSpawnScheduler.RollRespawnTicks.
        var minPursuers = (int)template.FollowInfo1;
        var maxPursuers = (int)template.FollowInfo2;
        var pursuerCapacity = maxPursuers > minPursuers
            ? minPursuers + rng.NextInt32(maxPursuers - minPursuers + 1)
            : minPursuers;

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
            PosZ = homeZ,
            InstanceId = instanceId,
            PursuerCapacity = pursuerCapacity
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

    /// <summary>
    ///     Accrues one hit's damage onto <paramref name="attackerCharacterId" />'s own tracked entry (legacy
    ///     <c>SetAttackInfoWithAvatar</c>, <c>Server/ts25zone/S07_MyGame05.cpp:1675-1720</c>), creating it at
    ///     zero cumulative damage first if this is that identity+session pair's first tracked hit. Safe to
    ///     call from any thread (see class remarks).
    /// </summary>
    /// <param name="attackerCharacterId">Identity half of the legacy identity+session slot key.</param>
    /// <param name="sessionToken">
    ///     Session half of the slot key -- see <see cref="MonsterAttackDamageEntry.SessionToken" />'s own
    ///     remarks for why a <see cref="PlayerRuntimeState" /> reference fills this role.
    /// </param>
    /// <param name="damage">
    ///     Negative/zero contributes no damage and registers no entry, same convention as
    ///     <see cref="TakeDamage" />.
    /// </param>
    internal void RegisterAttackDamage(int attackerCharacterId, object sessionToken, int damage)
    {
        if (damage <= 0)
            return;

        lock (_attackDamageLock)
        {
            var existing = _attackDamage.Find(e =>
                e.CharacterId == attackerCharacterId && ReferenceEquals(e.SessionToken, sessionToken));

            if (existing is not null)
            {
                existing.CumulativeDamage += damage;
                return;
            }

            // FIFO eviction: once full, the oldest identity+session slot -- and its whole accumulated total
            // -- is discarded to make room, never a partial/averaged carry-over (S07_MyGame05.cpp:1675-1720,
            // MAX_MONSTER_OBJECT_ATTACK_NUM == 50).
            if (_attackDamage.Count >= MaxAttackDamageEntries)
                _attackDamage.RemoveAt(0);

            _attackDamage.Add(new MonsterAttackDamageEntry
            {
                CharacterId = attackerCharacterId,
                SessionToken = sessionToken,
                CumulativeDamage = damage
            });
        }
    }

    /// <summary>
    ///     Oldest-to-newest snapshot of every currently-tracked attacker's cumulative damage -- a copy, not a
    ///     live view, so <see cref="Zone" />'s kill-credit scan never races a concurrent
    ///     <see cref="RegisterAttackDamage" /> call from another thread. Oldest-first ordering is load-bearing:
    ///     it is what lets a strictly-greater-than scan resolve an exact-tie in favor of the earliest-tracked
    ///     entry, matching legacy's own comparison in <c>SelectAvatarIndexForMaxAttackDamage</c>.
    /// </summary>
    internal IReadOnlyList<MonsterAttackDamageEntry> SnapshotAttackDamage()
    {
        lock (_attackDamageLock)
        {
            return _attackDamage.ToArray();
        }
    }
}
