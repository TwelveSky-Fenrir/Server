using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     Per-tick monster AI (<c>Server/ts25zone/S07_MyGame05.cpp</c>'s <c>MONSTER_OBJECT::Update</c>): a
///     simplified FSM covering spawn-wait, proximity aggro detection, pursuit that gives up once the target
///     escapes the monster's own detection radius (routing back to idle re-detection, not straight home -- see
///     <see cref="RunDecision" />), a windup-timed attack state (melee, and a Zone175-boss ranged variant), a
///     hit-stagger state, and forced return-to-spawn. One instance per <see cref="Zone" />.
/// </summary>
/// <remarks>
///     Deliberately not ported:
///     <list type="bullet">
///         <item>
///             <see cref="RunChase" />'s give-up guard is NOT a distance-from-home leash. Legacy's own chase
///             give-up guard (<c>S07_MyGame05.cpp:1359-1366</c>) and the identical-basis aggro-list pruning
///             check (<c>AdjustValidAttackTarget</c>, <c>:387-391</c>) both compare the monster's own CURRENT
///             position against the TARGET's CURRENT position, against the monster's "large" detection radius
///             (<see cref="Fenrir.Data.World.MonsterRowDto.RadiusInfo2" />) -- never against the monster's
///             home/spawn position. An earlier revision of this class used a spawn-region-radius-derived
///             <see cref="MonsterEntity.LeashRadius" /> distance-from-home bound with no legacy citation for
///             it; that has been replaced with the cited current-position-to-target-position check (see the
///             monster-ai-aggro-pathing finding). <see cref="MonsterEntity.LeashRadius" /> itself is retained
///             on the entity only because every spawn call site already populates it -- it is no longer read
///             by any give-up check here.
///         </item>
///         <item>
///             Monster-initiated damage fires via <see cref="Zone.ResolveMonsterAttack" /> (
///             <see cref="Combat.MonsterCombatResolver.ResolveMvpAttack" />).
///         </item>
///         <item>
///             Guard/tower/tribe-symbol-guard special AI recipes (legacy <c>mSpecialSortNumber</c> 1-6: tower
///             attacks, guard-attack target selection, throw-car idle AI) -- not modeled; those hang off a
///             per-slot "recipe" field this schema does not catalog. Only the SpecialType-gated Zone175-boss
///             ranged branch (<see cref="MonsterAiState.RangedAttackWindup" />) and the universal death/attack-
///             stone state (<see cref="MonsterAiState.Dead" />, tribe-symbol resolution) are covered -- see
///             <see cref="RunChase" /> and <see cref="MonsterSpawnScheduler.ProcessDeath" /> respectively.
///         </item>
///         <item>
///             <see cref="MonsterAiState.Flinch" />'s entry condition (a big single hit interrupting whatever
///             the monster was doing) is not wired -- it lives in <c>Zone.ApplyPvmAttack</c>, outside this
///             cluster's touched files this round. The state's own tick-countdown behavior is fully implemented
///             and tested; only the trigger is a follow-up.
///         </item>
///         <item>
///             UPDATE (2026-07, zone-transfer-in-progress-gate behavior contract): <see cref="TryAcquireTarget" />'s
///             candidate-eligibility filter now DOES exclude a mid-CROSS-SHARD-transfer avatar (legacy
///             <c>IsMovingZone()</c>, <c>S07_MyGame05.cpp:144-151</c>) via
///             <see cref="PlayerRuntimeState.IsMovingZone" /> -- previously listed here as not ported. A
///             SAME-shard handoff still needs no such check: Fenrir's in-process transfer removes a player
///             from this zone's own player map/AOI grid before the target zone ever adds them, so there is no
///             observable window where a same-shard mid-transfer player could appear as a candidate here --
///             only the cross-shard case (where the character stays live in this zone's own player map for
///             the whole real-world window until its actual disconnect) needed the new field. Still NOT
///             ported: "hiding" (legacy <c>IsHiding()</c>) has no equivalent anywhere in
///             <see cref="PlayerRuntimeState" /> yet (a separate, unimplemented gameplay feature). The
///             action-sort-state-0/33 exclusion (<c>:156-159</c>) and the dead, never-compiled tribe-guard
///             block (<c>:160-167</c>, <c>//#define USE_WAR_GUARD</c>) are likewise not ported -- the former
///             has no cataloged Fenrir equivalent state, the latter is correctly never-real legacy behavior
///             in the first place.
///         </item>
///     </list>
/// </remarks>
public sealed class MonsterAiSystem(IRandomSource? random = null) : ISimulationSystem
{
    /// <summary>One legacy tick's worth of movement time, matching the report's own "vitesse × dTime" with dTime ≈ 0.5 s.</summary>
    private const float TickSeconds = SimulationClock.LegacyTickMilliseconds / 1000f;

    /// <summary>Close enough to "arrived" that jitter/overshoot never leaves a monster oscillating around its destination.</summary>
    private const float ArrivalEpsilon = 1f;

    /// <summary>Minimum idle-wander destination radius (S07_MyGame05.cpp:1048): <c>50 + rand()%51</c>, i.e. [50,100].</summary>
    private const float WanderMinRadius = 50f;

    /// <summary>
    ///     Idle-wander radius roll span -- the exclusive upper bound of the <c>rand()%51</c> in
    ///     <see cref="WanderMinRadius" />'s citation.
    /// </summary>
    private const int WanderRadiusRollSpan = 51;

    /// <summary>
    ///     Idle-wander direction component roll span (S07_MyGame05.cpp:1039-1040): legacy draws each axis from
    ///     a continuous <c>RandomNumber(-100.0f, 100.0f)</c>; ported as an integer roll over [-100,100] via
    ///     <see cref="IRandomSource" />'s single-draw-per-call-site convention (see class remarks) rather than
    ///     introducing a float-random member this codebase's <see cref="IRandomSource" /> doesn't expose.
    /// </summary>
    private const int WanderDirectionRollSpan = 201;

    private const int WanderDirectionRollHalfSpan = 100;

    /// <summary>
    ///     Minimum resolved displacement required to actually commit to an idle-wander destination
    ///     (S07_MyGame05.cpp:1053).
    /// </summary>
    private const float WanderMinDisplacement = 50f;

    /// <summary>
    ///     Draws <c>SelectAvatarIndexForPossibleAttack</c>'s per-candidate coin flip (<c>rand_mir()%2==0</c>,
    ///     <c>S07_MyGame05.cpp:208-213</c>). One shared instance across every zone this DI singleton ticks for
    ///     (see <c>DomainServiceCollectionExtensions</c>'s registration) -- safe because the default
    ///     <see cref="SystemRandomSource" /> wraps the thread-safe <see cref="Random.Shared" />.
    /// </summary>
    private readonly IRandomSource _random = random ?? SystemRandomSource.Instance;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var dt = TickSeconds * legacyTicksElapsed;

        // Captured once per call and threaded down to the anti-clump pursuer count instead of re-reading
        // zone.MonstersSnapshot per candidate: the property itself already allocates a snapshot, so re-fetching
        // it inside a per-candidate check would multiply that allocation by candidate count x monster count.
        var monsters = zone.MonstersSnapshot;

        foreach (var monster in monsters)
            Update(zone, monster, dt, legacyTicksElapsed, monsters);
    }

    private void Update(Zone zone, MonsterEntity monster, float dt, int legacyTicksElapsed,
        IEnumerable<MonsterEntity> allMonsters)
    {
        switch (monster.AiState)
        {
            case MonsterAiState.Spawning:
                monster.StateTicks++;
                if (monster.StateTicks >= Math.Max(1, (int)monster.Template.FrameInfo1))
                {
                    monster.AiState = MonsterAiState.Decision;
                    monster.StateTicks = 0;
                }

                break;

            case MonsterAiState.Decision:
                RunDecision(zone, monster, legacyTicksElapsed, allMonsters);
                break;

            case MonsterAiState.Patrol:
                // A004 (S07_MyGame05.cpp:1300-1344): walks toward the random wander point RunDecision just
                // rolled (MonsterEntity.WanderTargetX/Z, legacy aTargetLocation) -- NOT home; A004 never
                // references mFirstLocation at all.
                MoveToward(zone, monster, monster.WanderTargetX, monster.WanderTargetZ, monster.Template.WalkSpeed,
                    dt);
                if (DistanceSquared(monster.PosX, monster.PosZ, monster.WanderTargetX, monster.WanderTargetZ) <=
                    ArrivalEpsilon * ArrivalEpsilon)
                {
                    monster.AiState = MonsterAiState.Decision;
                    monster.StateTicks = 0;
                }
                else
                {
                    TryAcquireTarget(zone, monster, legacyTicksElapsed,
                        allMonsters); // re-checked every tick -- a wandering monster can still be aggroed
                }

                break;

            case MonsterAiState.Chase:
                RunChase(zone, monster, dt);
                break;

            case MonsterAiState.AttackWindup:
                monster.StateTicks++;
                if (monster.StateTicks == 1 && monster.TargetCharacterId is { } attackTargetId)
                    zone.ResolveMonsterAttack(monster, attackTargetId);

                if (monster.StateTicks >= Math.Max(1, (int)monster.Template.FrameInfo3))
                {
                    monster.AiState = MonsterAiState.Decision;
                    monster.StateTicks = 0;
                }

                break;

            case MonsterAiState.RangedAttackWindup:
                monster.StateTicks++;
                if (monster.StateTicks >= Math.Max(1, (int)monster.Template.FrameInfo4))
                {
                    monster.AiState = MonsterAiState.Decision;
                    monster.StateTicks = 0;
                }

                break;

            case MonsterAiState.Flinch:
                monster.StateTicks++;
                if (monster.StateTicks >= Math.Max(1, (int)monster.Template.FrameInfo2))
                {
                    monster.AiState = MonsterAiState.Decision;
                    monster.StateTicks = 0;
                }

                break;

            case MonsterAiState.ReturnToSpawn:
                // A020 (Server/ts25zone/S07_MyGame05.cpp:1658-1673): a fixed, distance-independent
                // animation-frame delay -- NOT a walk. Legacy accumulates an animation-frame counter
                // (aFrame += tPostTime*30) until it reaches this monster type's own mFrameInfo[5]
                // (Template.FrameInfo6) threshold, then teleports straight to mFirstLocation and resets
                // to the post-spawn wait state (aSort 0) -- no movement/pathing call exists anywhere in
                // that handler, verified directly against source. See the legacy-behavior-translator
                // contract for `monster-ai-aggro-pathing` (2026-07 round) for the full citation trail.
                monster.StateTicks++;
                if (monster.StateTicks >= Math.Max(1, (int)monster.Template.FrameInfo6))
                {
                    monster.PosX = monster.HomeX;
                    monster.PosY = monster.HomeY;
                    monster.PosZ = monster.HomeZ;
                    monster.TargetCharacterId = null;
                    monster.AiState = MonsterAiState.Spawning; // aSort 19 -> teleport home -> aSort 0
                    monster.StateTicks = 0;
                }

                break;

            case MonsterAiState.Dead:
                break; // transient -- removed from the pool before the next tick drains it
        }
    }

    /// <summary>
    ///     A002 idle/decision dispatcher (<c>S07_MyGame05.cpp:1003-1057</c>): every idle tick first re-attempts
    ///     the same wide-radius proximity scan used for original aggro -- including the very first idle tick
    ///     after this monster just gave up a chase target (<see cref="RunChase" />'s two give-up branches both
    ///     land here, not in <see cref="MonsterAiState.ReturnToSpawn" />). Only once 60 continuous idle ticks
    ///     pass with zero re-engagement does the monster even consider heading home; until then it falls into
    ///     a 40-second random-wander cadence instead, still loitering wherever it currently is.
    /// </summary>
    private void RunDecision(Zone zone, MonsterEntity monster, int legacyTicksElapsed,
        IEnumerable<MonsterEntity> allMonsters)
    {
        if (TryAcquireTarget(zone, monster, legacyTicksElapsed, allMonsters))
            return;

        // 60-second in-place re-detection grace period (mCheckFirstLocationTime, S07_MyGame05.cpp:1012-1031):
        // only after this many continuous idle ticks with no re-acquired target does the monster even consider
        // heading home -- and even then only if it isn't already there. Crossing this threshold always resets
        // the timer, whether or not the distance check below actually starts a return this same tick
        // (S07_MyGame05.cpp:1014 has no guard on that reset).
        monster.IdleReturnElapsedTicks += legacyTicksElapsed;
        if (monster.IdleReturnElapsedTicks > SimulationClock.MonsterIdleReturnHomeLegacyTicks)
        {
            monster.IdleReturnElapsedTicks = 0;

            if (DistanceSquared(monster.PosX, monster.PosZ, monster.HomeX, monster.HomeZ) >
                ArrivalEpsilon * ArrivalEpsilon)
            {
                monster.AiState = MonsterAiState.ReturnToSpawn;
                monster.StateTicks = 0;
                monster.IdleWanderElapsedTicks = 0; // S07_MyGame05.cpp:1024 -- also reset once a return actually begins
                return;
            }

            // Already home: legacy has no `return` on this branch (S07_MyGame05.cpp:1022-1032), so it falls
            // through to the 40-second wander check below in this SAME tick instead of stopping here -- an
            // idle monster sitting at home can still start a wander this same tick if its own 40s timer has
            // separately elapsed.
        }

        // 40-second random-wander fallback (mCheckLastWalkTime, S07_MyGame05.cpp:1033-1057), reached whenever
        // the 60-second branch above did not itself just start a return home this tick.
        monster.IdleWanderElapsedTicks += legacyTicksElapsed;
        if (monster.IdleWanderElapsedTicks < SimulationClock.MonsterIdleWanderLegacyTicks)
            return;

        monster.IdleWanderElapsedTicks = 0;

        if (!TryComputeWanderDestination(zone, monster, out var destX, out var destZ))
            return; // resolved displacement fell short of the 50-unit minimum -- discarded, no observable effect

        monster.WanderTargetX = destX;
        monster.WanderTargetZ = destZ;
        monster.AiState = MonsterAiState.Patrol;
        monster.StateTicks = 0;
    }

    /// <summary>
    ///     Random wander destination (S07_MyGame05.cpp:1039-1053): a normalized random 2D direction scaled by a
    ///     radius uniformly drawn from [50,100], measured from the monster's CURRENT position -- not home.
    ///     Legacy resolves the raw random point through <c>mWORLD.Path</c> (navmesh-aware, can slide the
    ///     result short of an obstacle) before measuring displacement; Fenrir has no equivalent path-resolution
    ///     step, so an unwalkable raw destination is simply treated as unreachable (same posture as
    ///     <see cref="MoveToward" />'s own "no pathfinding around obstacles" simplification elsewhere in this
    ///     class) rather than sliding to a partial point.
    /// </summary>
    private bool TryComputeWanderDestination(Zone zone, MonsterEntity monster, out float destX, out float destZ)
    {
        var dirX = (float)(_random.NextInt32(WanderDirectionRollSpan) - WanderDirectionRollHalfSpan);
        var dirZ = (float)(_random.NextInt32(WanderDirectionRollSpan) - WanderDirectionRollHalfSpan);
        var length = MathF.Sqrt(dirX * dirX + dirZ * dirZ);
        if (length > 0f)
        {
            dirX /= length;
            dirZ /= length;
        }

        var radius = WanderMinRadius + _random.NextInt32(WanderRadiusRollSpan);
        destX = monster.PosX + dirX * radius;
        destZ = monster.PosZ + dirZ * radius;

        if (zone.Geometry is { } geometry && !geometry.IsWalkable(destX, destZ))
        {
            // Unreachable -- collapse to zero displacement so the check below always discards it, same as
            // legacy's own path-resolution falling short of the raw random point.
            destX = monster.PosX;
            destZ = monster.PosZ;
        }

        return DistanceSquared(monster.PosX, monster.PosZ, destX, destZ) >=
               WanderMinDisplacement * WanderMinDisplacement;
    }

    /// <summary>
    ///     <c>SelectAvatarIndexForPossibleAttack</c> (<c>S07_MyGame05.cpp:113-216</c>): proactive aggro gated to
    ///     <c>mAttackType ∈ {1,3,6}</c>, throttled to once every
    ///     <see cref="SimulationClock.MonsterDetectionThrottleLegacyTicks" />
    ///     legacy ticks (~1 s, <c>mCheckDetectEnemyTime</c>, resets on every attempted check -- successful or
    ///     not), with detection radius <see cref="Fenrir.Data.World.MonsterRowDto.RadiusInfo2" /> (not
    ///     <see cref="Fenrir.Data.World.MonsterRowDto.RadiusInfo1" />, the smaller melee-range radius
    ///     <see cref="RunChase" /> uses for the attack-windup transition -- do not swap these two), gated to at
    ///     least 1, a per-candidate anti-clump pursuer cap, and a 50% coin flip.
    /// </summary>
    private bool TryAcquireTarget(Zone zone, MonsterEntity monster, int legacyTicksElapsed,
        IEnumerable<MonsterEntity> allMonsters)
    {
        if (monster.Template.AttackType is not (1 or 3 or 6))
            return false;

        // 1-second detection throttle (mCheckDetectEnemyTime, S07_MyGame05.cpp:127-131): restarts on every
        // attempted check, whether or not it ends up finding a candidate -- a monster that rolls no target
        // this pass must wait a full window again, even with a valid target still in range the whole time.
        monster.DetectionThrottleTicks += legacyTicksElapsed;
        if (monster.DetectionThrottleTicks < SimulationClock.MonsterDetectionThrottleLegacyTicks)
            return false;

        monster.DetectionThrottleTicks = 0;

        var detectionRadius = monster.Template.RadiusInfo2;
        if (detectionRadius <= 0)
            return false; // S07_MyGame05.cpp:132-135 -- a non-positive configured radius never detects anyone

        var detectionRadiusSq = (float)detectionRadius * detectionRadius;

        foreach (var characterId in zone.NeighborsOfPosition(monster.PosX, monster.PosZ))
        {
            // Ready-state, then IsMovingZone(), then (unmodeled) hiding, then death -- legacy's own ordering
            // (S07_MyGame05.cpp:136-152). Only observably reachable for a CROSS-shard transfer; a same-shard
            // handoff already removes the candidate from this zone's own player map before this loop could
            // ever see it -- see PlayerRuntimeState.IsMovingZone's own remarks.
            if (!zone.TryGetPlayer(characterId, out var player) || player is null || player.IsMovingZone ||
                player.IsDead)
                continue;

            // Zone-241 "LOD" personal-instance aggro-eligibility filter (Server/ts25zone/S07_MyGame05.cpp:169-177,565):
            // a tagged personal boss (monster.InstanceId non-null) never considers an avatar outside its own instance.
            if (monster.InstanceId is { } requiredInstanceId && player.DungeonInstanceId != requiredInstanceId)
                continue;

            if (DistanceSquared(monster.PosX, monster.PosZ, player.PosX, player.PosZ) > detectionRadiusSq)
                continue;

            // Anti-clump cap (S07_MyGame05.cpp:186-207): count every OTHER live monster already chasing or
            // winding up an attack against this exact candidate; refuse to add another pursuer once that
            // count exceeds this monster's own rolled capacity minus one (MonsterEntity.PursuerCapacity,
            // rolled once at spawn -- S10_MySummon.cpp:795).
            if (CountOtherPursuers(allMonsters, monster, characterId) > monster.PursuerCapacity - 1)
                continue;

            // Coin flip (S07_MyGame05.cpp:208-213): only a 50% roll actually selects this candidate -- a lost
            // flip skips it for the rest of THIS call; it is never revisited until the next throttle-permitted
            // check (a full window later).
            if (_random.NextInt32(2) != 0)
                continue;

            monster.TargetCharacterId = characterId;
            RecordAggro(monster, characterId);
            monster.AiState = MonsterAiState.Chase;
            monster.StateTicks = 0;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Anti-clump pursuer count (S07_MyGame05.cpp:186-207): every OTHER live monster already locked onto
    ///     <paramref name="candidateCharacterId" /> while chasing or winding up an attack (legacy aSort 4/5).
    ///     <see cref="MonsterAiState.RangedAttackWindup" /> counts as "attacking" too -- Fenrir splits legacy's
    ///     single ranged-attack behavior into its own state, but it is the same actively-engaged-with-target
    ///     phase an anti-clump count is meant to capture.
    /// </summary>
    private static int CountOtherPursuers(IEnumerable<MonsterEntity> allMonsters, MonsterEntity monster,
        int candidateCharacterId)
    {
        var count = 0;
        foreach (var other in allMonsters)
        {
            if (other.ServerIndex == monster.ServerIndex)
                continue;

            if (other.AiState is not (MonsterAiState.Chase or MonsterAiState.AttackWindup
                or MonsterAiState.RangedAttackWindup))
                continue;

            if (other.TargetCharacterId == candidateCharacterId)
                count++;
        }

        return count;
    }

    /// <summary>Bounded FIFO, oldest purged first (legacy cap 50).</summary>
    private static void RecordAggro(MonsterEntity monster, int characterId)
    {
        var list = monster.AggroCharacterIds;
        if (list.Contains(characterId))
            return;

        if (list.Count >= 50)
            list.RemoveAt(0);

        list.Add(characterId);
    }

    private void RunChase(Zone zone, MonsterEntity monster, float dt)
    {
        if (monster.TargetCharacterId is not { } targetId || !zone.TryGetPlayer(targetId, out var target) ||
            target is null || target.IsDead)
        {
            // A005: an invalid/disconnected/unique-number-mismatched/dead chased target routes to the
            // idle/decision state, NOT straight to return-to-spawn (S07_MyGame05.cpp:1351-1358). The very
            // next idle tick immediately re-attempts the same wide-radius proximity scan used for original
            // aggro (RunDecision -> TryAcquireTarget) at the monster's CURRENT position, and only gives up and
            // heads home after a continuous 60-second grace period with zero re-engagement -- see RunDecision.
            monster.TargetCharacterId = null;
            monster.AiState = MonsterAiState.Decision;
            monster.StateTicks = 0;
            return;
        }

        var distanceToTargetSq = DistanceSquared(monster.PosX, monster.PosZ, target.PosX, target.PosZ);

        // Give-up guard (S07_MyGame05.cpp:1359-1366; identical basis in AdjustValidAttackTarget, :387-391):
        // squared distance from the monster's OWN CURRENT position to the target's CURRENT position, against
        // the monster's "large" detection radius (RadiusInfo2) -- NOT a distance-from-home leash. Legacy's
        // chase give-up never references the monster's home/spawn position at all; because the monster is
        // always closing on its target while chasing, this practically only trips if the target outruns the
        // monster or teleports away. Same A005 destination as the invalid-target branch above: idle/decision,
        // not return-to-spawn (S07_MyGame05.cpp:1361 sets aSort=1) -- see RunDecision for the grace period.
        var detectionRadiusSq = (float)monster.Template.RadiusInfo2 * monster.Template.RadiusInfo2;
        if (distanceToTargetSq > detectionRadiusSq)
        {
            monster.TargetCharacterId = null;
            monster.AiState = MonsterAiState.Decision;
            monster.StateTicks = 0;
            return;
        }

        // Attack-windup transition threshold uses RadiusInfo1 (melee range), distinct from the more lenient
        // RadiusInfo2 give-up/detection radius above -- do not swap these two.
        var attackRadiusSq = (float)monster.Template.RadiusInfo1 * monster.Template.RadiusInfo1;
        if (distanceToTargetSq <= attackRadiusSq)
        {
            monster.AiState = MonsterAiState.AttackWindup;
            monster.StateTicks = 0;
            return;
        }

        // Zone175-type boss (A002/A005_FOR_ZONE_175_TYPE_BOSS, S07_MyGame05.cpp:1176-1298): can also loose a
        // ranged attack from its full detection radius instead of always closing to melee range first. Legacy
        // rolls a 1/3 chance each tick between this and continuing to close in, over a multi-candidate
        // distance-banded target list; simplified here to "always take the ranged opening once in range",
        // since Fenrir's single-locked-target Chase has no equivalent candidate list to roll over. The
        // give-up guard above already guarantees distanceToTargetSq <= detectionRadiusSq by this point.
        if (IsZone175TypeBoss(monster.Template.SpecialType) && distanceToTargetSq <= detectionRadiusSq)
        {
            monster.AiState = MonsterAiState.RangedAttackWindup;
            monster.StateTicks = 0;
            return;
        }

        MoveToward(zone, monster, target.PosX, target.PosZ, monster.Template.RunSpeed, dt);
    }

    /// <summary><c>mSpecialType</c> 40-44 (S07_MyGame05.cpp:59-92): the 5 seeded "elite boss" monsters (564-568).</summary>
    private static bool IsZone175TypeBoss(byte specialType)
    {
        return specialType is >= 40 and <= 44;
    }

    /// <summary>
    ///     Straight-line step of <c>speed * dt</c> toward (targetX, targetZ). Validated against
    ///     <see cref="Zone.Geometry" /> when loaded; when no <c>.WM</c> is loaded, the step is applied
    ///     unconditionally, same posture as <see cref="Movement.MovementRules" /> for players.
    /// </summary>
    private static void MoveToward(Zone zone, MonsterEntity monster, float targetX, float targetZ, float speed,
        float dt)
    {
        var dx = targetX - monster.PosX;
        var dz = targetZ - monster.PosZ;
        var distance = MathF.Sqrt(dx * dx + dz * dz);
        if (distance <= 0.0001f)
            return;

        var step = speed * dt;
        if (step <= 0f)
            return;

        float newX, newZ;
        if (step >= distance)
        {
            newX = targetX;
            newZ = targetZ;
        }
        else
        {
            newX = monster.PosX + dx / distance * step;
            newZ = monster.PosZ + dz / distance * step;
        }

        if (zone.Geometry is { } geometry)
        {
            if (!geometry.IsWalkable(newX, newZ))
                return; // no pathfinding around obstacles in this pass -- simply refuse the blocked step

            if (geometry.TryGetGroundHeight(newX, newZ, out var groundY))
                monster.PosY = groundY;
        }

        monster.PosX = newX;
        monster.PosZ = newZ;
        monster.Heading = MathF.Atan2(dx, dz);
    }

    private static float DistanceSquared(float x1, float z1, float x2, float z2)
    {
        var dx = x1 - x2;
        var dz = z1 - z2;
        return dx * dx + dz * dz;
    }
}
