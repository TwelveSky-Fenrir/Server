using System.Numerics;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.Pathfinding;

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
///             UPDATE: <see cref="MonsterAiState.Flinch" />'s entry condition (a big single hit interrupting
///             whatever the monster was doing) is now wired via <c>Zone.TryApplyPvmFlinch</c>, called from
///             <c>Zone.ApplyPvmAttack</c> on a landed, non-killing hit -- previously listed here as an open
///             trigger gap. The state's own tick-countdown behavior below was already fully implemented and
///             tested before this.
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
///         <item>
///             UPDATE (2026-07, shared-acquisition/damage-table behavior contract): a successful
///             <see cref="TryAcquireTarget" /> now zero-seeds a slot for the acquired candidate in the SAME
///             50-capacity attacker table <see cref="MonsterEntity.RegisterAttackDamage" />/kill-credit
///             resolution use (<see cref="MonsterEntity.RegisterAcquisition" />), matching legacy's single
///             shared <c>SetAttackInfoWithAvatar</c> array (<c>S07_MyGame05.cpp:1675-1720</c>, written from
///             both the acquisition call sites at <c>:1068</c>/<c>:1321</c> and the real-damage call site at
///             <c>S07_MyGame02.cpp:2167</c>) -- previously routed into a separate, acquisition-only FIFO list
///             that kill-credit never read. The two algorithms stay separate code paths (this first-found
///             linear scan here vs. <see cref="Zone.SelectDamageBasedKillCredit" />'s FIFO-capped max-damage
///             lookup); only the underlying table is now shared, as it is in legacy. NOT further ported by
///             this fix: a third, structurally distinct acquisition-like function,
///             <c>SelectAvatarIndexForPossibleAttackForZone175TypeBoss</c> (<c>S07_MyGame05.cpp:218-265</c>),
///             also calls the shared-table writer directly (multi-candidate, no anti-clump/break-on-first-
///             success, unlike this method) for a boss-specific mechanic this class does not model as its own
///             code path; and the full category (<c>mSpecialSortNumber</c>) scope of both the acquisition
///             write-through and the real-damage write gate was not exhaustively re-audited -- both flagged
///             for <c>cpp-zone-gameplay-analyst</c> re-check rather than assumed complete.
///         </item>
///     </list>
/// </remarks>
public sealed class MonsterAiSystem(IRandomSource? random = null) : ISimulationSystem
{
    /// <summary>One legacy tick's worth of movement time, matching the report's own "vitesse × dTime" with dTime ≈ 0.5 s.</summary>
    private const float TickSeconds = SimulationClock.LegacyTickMilliseconds / 1000f;

    /// <summary>Close enough to "arrived" that jitter/overshoot never leaves a monster oscillating around its destination.</summary>
    private const float ArrivalEpsilon = 1f;

    /// <summary>
    ///     How far the movement goal (a chased avatar's live position, or a wander point) must relocate from the
    ///     goal a cached navmesh route was computed for before that route is re-planned. A Fenrir-chosen
    ///     heuristic with no legacy analogue (legacy had no A* route to invalidate): large enough that a chased
    ///     target's small per-tick drift does not burn the per-tick pathfinding budget on a near-identical route
    ///     every tick, small enough that pursuit never trails the target by a visibly stale margin. Well under a
    ///     minimum wander step (<see cref="WanderMinDisplacement" />) so a fresh wander destination always
    ///     triggers a plan.
    /// </summary>
    private const float PathReplanGoalMoveThreshold = 40f;

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

        // One monster-pathfinding budget window per simulation pass (see GameServerOptions.
        // MonsterPathfindingBudgetPerTick / MonsterPathfinder): monsters past the budget this pass reuse their
        // cached waypoints or step straight-line rather than blocking. No-op on a zone with no geometry (or one
        // whose pathfinder has not been lazily built yet).
        zone.ResetPathBudget();

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
                    monster.ReleaseTarget();
                    monster.AiState = MonsterAiState.Spawning; // aSort 19 -> teleport home -> aSort 0
                    monster.StateTicks = 0;

                    // Legacy A020 broadcasts the teleport-home + reset-to-idle frame (aSort 0 at mFirstLocation)
                    // unconditionally via B_MONSTER_ACTION_RECV(...,1)+Send1 (S07_MyGame05.cpp:1666-1672) -- fires
                    // at the new (home) position, so players near home see the monster reappear idle there.
                    zone.BroadcastMonsterActionChange(monster);
                }

                break;

            case MonsterAiState.Dead:
                break; // transient -- removed from the pool before the next tick drains it
        }

        // Keeps Zone's monster-side AOI grid in sync with wherever this pass just left the monster (Patrol/
        // Chase's MoveToward step, or ReturnToSpawn's teleport-home branch above) -- a cheap no-op on every
        // other state, since none of them mutate PosX/PosZ. See Zone.SyncMonsterCell's own remarks.
        zone.SyncMonsterCell(monster);
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

                // Legacy A002 broadcasts the return-home transition (aSort 19) via
                // B_MONSTER_ACTION_RECV(...,1)+Send1 (S07_MyGame05.cpp:1025-1028).
                zone.BroadcastMonsterActionChange(monster);
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
        // Mirror the wander destination into the broadcast target descriptor (legacy aSort=3 sets
        // aTargetLocation = tPossibleLocation, S07_MyGame05.cpp:1059-1061) so a wandering monster renders
        // walking toward the point it actually walks to.
        monster.TargetLocationX = destX;
        monster.TargetLocationY = monster.PosY;
        monster.TargetLocationZ = destZ;
        monster.AiState = MonsterAiState.Patrol;
        monster.StateTicks = 0;

        // Legacy A002 broadcasts the wander transition (aSort 3) via B_MONSTER_ACTION_RECV(...,1)+Send1
        // (S07_MyGame05.cpp:1057-1064).
        zone.BroadcastMonsterActionChange(monster);
    }

    /// <summary>
    ///     Random wander destination (S07_MyGame05.cpp:1039-1053): a normalized random 2D direction scaled by a
    ///     radius uniformly drawn from [50,100], measured from the monster's CURRENT position -- not home.
    ///     Legacy resolves the raw random point through <c>mWORLD.Path</c> (navmesh-aware, can slide the
    ///     result short of an obstacle) before measuring displacement; Fenrir instead discards an unwalkable raw
    ///     destination here (treated as unreachable), then lets <see cref="MoveToward" />'s A* + funnel router
    ///     find an obstacle-avoiding route to any accepted (walkable) wander point -- so the wander <em>walk</em>
    ///     is now navmesh-aware even though the destination <em>roll</em> is still a discard-if-blocked check.
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

            // Capture the full target descriptor together (index + unique number + location), matching
            // legacy's single-beat aTargetObjectIndex/aTargetObjectUniqueNumber/aTargetLocation write at the
            // chase transition (S07_MyGame05.cpp:1104-1105,1166-1171,1373-1374).
            monster.AssignTarget(characterId, player.UniqueNumber, player.PosX, player.PosY, player.PosZ);

            // Acquisition write-through (SetAttackInfoWithAvatar called with a zero damage argument from the
            // acquisition call sites, S07_MyGame05.cpp:1068/:1321): zero-seeds a slot for this candidate in
            // the SAME shared attacker table RegisterAttackDamage/kill-credit resolution use, not a separate
            // proximity-only list -- see MonsterEntity.RegisterAcquisition's own remarks for why this is a
            // distinct call into a shared table rather than a merge of the two algorithms.
            monster.RegisterAcquisition(characterId, player);
            monster.AiState = MonsterAiState.Chase;
            monster.StateTicks = 0;

            // Legacy broadcasts the aggro transition (aSort 4) immediately via B_MONSTER_ACTION_RECV(...,1)+
            // Send1 (S07_MyGame05.cpp:1164-1173) -- this is the frame a real client needs to start rendering
            // the chase toward a correctly-identified target, instead of only finding out via the keep-alive.
            zone.BroadcastMonsterActionChange(monster);
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
            monster.ReleaseTarget();
            monster.AiState = MonsterAiState.Decision;
            monster.StateTicks = 0;

            // Legacy A005 broadcasts the drop-to-idle (aSort 1) on an invalid/dead target via
            // B_MONSTER_ACTION_RECV(...,1)+Send1 (S07_MyGame05.cpp:1351-1356).
            zone.BroadcastMonsterActionChange(monster);
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
            monster.ReleaseTarget();
            monster.AiState = MonsterAiState.Decision;
            monster.StateTicks = 0;

            // Legacy A005 broadcasts the give-up-to-idle (aSort 1) when the target drifts out of range via
            // B_MONSTER_ACTION_RECV(...,1)+Send1 (S07_MyGame05.cpp:1359-1364).
            zone.BroadcastMonsterActionChange(monster);
            return;
        }

        // Keep the broadcast target descriptor tracking the pursued avatar's live position while actively
        // engaged (legacy re-derives aTargetLocation toward the target each chase pass) so the keep-alive and
        // the next state-change frame both carry a fresh waypoint, never a stale chase-start one.
        monster.TargetLocationX = target.PosX;
        monster.TargetLocationY = target.PosY;
        monster.TargetLocationZ = target.PosZ;

        // Attack-windup transition threshold uses RadiusInfo1 (melee range), distinct from the more lenient
        // RadiusInfo2 give-up/detection radius above -- do not swap these two.
        var attackRadiusSq = (float)monster.Template.RadiusInfo1 * monster.Template.RadiusInfo1;
        if (distanceToTargetSq <= attackRadiusSq)
        {
            monster.AiState = MonsterAiState.AttackWindup;
            monster.StateTicks = 0;

            // Legacy A005 broadcasts the attack transition (aSort 5) via B_MONSTER_ACTION_RECV(...,1)+Send1
            // (S07_MyGame05.cpp:1370-1381).
            zone.BroadcastMonsterActionChange(monster);
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

            // Legacy A002_FOR_ZONE_175_TYPE_BOSS broadcasts the ranged-attack transition (aSort 7) via
            // B_MONSTER_ACTION_RECV(...,1)+Send3 (S07_MyGame05.cpp:1219-1228).
            zone.BroadcastMonsterActionChange(monster);
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
    ///     Advances the monster one movement step toward (targetX, targetZ). When no <c>.WM</c> geometry is
    ///     loaded the step is an unconstrained straight line (same posture as <see cref="Movement.MovementRules" />
    ///     for players -- unchanged from before this class gained pathfinding). When geometry is present the
    ///     monster follows an A* + funnel navmesh route (<see cref="MonsterPathfinder" />) cached on the entity,
    ///     routing <em>around</em> obstacles instead of stalling at a blocked step; the route is recomputed
    ///     (budget permitting) only when the goal has relocated, the cached path is exhausted, or the next
    ///     straight segment is blocked. A monster denied a recompute this tick keeps following its cached path,
    ///     or -- with no cached path -- steps straight-line-with-walkability-refusal (never teleporting through a
    ///     wall), the same conservative fallback used when no route can be found at all.
    /// </summary>
    private static void MoveToward(Zone zone, MonsterEntity monster, float targetX, float targetZ, float speed,
        float dt)
    {
        if (zone.Geometry is not { } geometry)
        {
            // No navmesh: unconstrained straight-line move, exactly as before (no walkability, no ground snap).
            StepToward(monster, targetX, targetZ, speed, dt, null, false);
            return;
        }

        if (zone.Pathfinder is { } pathfinder)
        {
            MoveAlongPath(pathfinder, geometry, monster, targetX, targetZ, speed, dt);
            return;
        }

        // Geometry present but pathfinding disabled (budget option 0): straight-line-with-walkability-refusal,
        // the pre-pathfinding fallback (refuse a blocked step rather than route around it).
        StepToward(monster, targetX, targetZ, speed, dt, geometry, true);
    }

    /// <summary>
    ///     Follows (or recomputes) the monster's cached navmesh route toward (targetX, targetZ). A recompute is
    ///     attempted only when needed -- goal relocated past <see cref="SimulationClock" />-independent
    ///     <see cref="PathReplanGoalMoveThreshold" />, cached path exhausted, or the next segment blocked -- and
    ///     only if the per-tick pathfinding budget permits; following an existing route costs no budget.
    /// </summary>
    private static void MoveAlongPath(MonsterPathfinder pathfinder, ZoneGeometry geometry, MonsterEntity monster,
        float targetX, float targetZ, float speed, float dt)
    {
        var exhausted = monster.WaypointCursor >= monster.PathWaypoints.Count;
        var needReplan = exhausted
                         || PathGoalMoved(monster, targetX, targetZ)
                         || NextStepBlocked(monster, geometry, speed, dt);

        if (needReplan)
        {
            if (pathfinder.TryConsumeBudget())
            {
                var from = new Vector3(monster.PosX, monster.PosY, monster.PosZ);
                var to = new Vector3(targetX, monster.PosY, targetZ);
                if (pathfinder.TryFindPath(from, to, monster.PathWaypoints))
                {
                    monster.WaypointCursor = 0;
                    monster.PathGoalX = targetX;
                    monster.PathGoalZ = targetZ;
                }
                else
                {
                    // Off-mesh endpoint / disconnected goal: don't teleport through walls -- straight-line step
                    // that refuses a blocked cell, same conservative posture as the pre-pathfinding behavior.
                    monster.ClearPath();
                    StepToward(monster, targetX, targetZ, speed, dt, geometry, true);
                    return;
                }
            }
            else if (exhausted)
            {
                // Over budget this tick with no usable cached path: straight-line fallback keeps last frame's
                // motion going and never blocks; a real route is computed on a later tick once budget frees up.
                StepToward(monster, targetX, targetZ, speed, dt, geometry, true);
                return;
            }

            // Over budget but a cached (possibly slightly stale) path is still available: keep following it.
        }

        FollowWaypoints(monster, geometry, speed, dt);
    }

    /// <summary>
    ///     Walks the monster along its cached waypoint list by up to <c>speed * dt</c> this tick, advancing the
    ///     cursor as waypoints are reached (and spending any leftover step distance toward the next one, so a
    ///     fast monster is not throttled to one sparse funnel corner per tick). Each committed position snaps to
    ///     ground height; funnel waypoints are on-mesh by construction, so no walkability refusal is needed here.
    /// </summary>
    private static void FollowWaypoints(MonsterEntity monster, ZoneGeometry geometry, float speed, float dt)
    {
        var remainingStep = speed * dt;
        if (remainingStep <= 0f)
            return;

        while (monster.WaypointCursor < monster.PathWaypoints.Count)
        {
            var waypoint = monster.PathWaypoints[monster.WaypointCursor];
            var dx = waypoint.X - monster.PosX;
            var dz = waypoint.Y - monster.PosZ; // Vector2.Y carries world Z
            var distance = MathF.Sqrt(dx * dx + dz * dz);

            if (distance <= ArrivalEpsilon)
            {
                monster.WaypointCursor++;
                continue;
            }

            monster.Heading = MathF.Atan2(dx, dz);

            if (remainingStep >= distance)
            {
                CommitPosition(monster, geometry, waypoint.X, waypoint.Y);
                remainingStep -= distance;
                monster.WaypointCursor++;
                continue;
            }

            var newX = monster.PosX + dx / distance * remainingStep;
            var newZ = monster.PosZ + dz / distance * remainingStep;
            CommitPosition(monster, geometry, newX, newZ);
            return;
        }
    }

    /// <summary>
    ///     Single straight-line step of <c>speed * dt</c> toward (destX, destZ). With no geometry, moves
    ///     unconditionally (no ground snap). With geometry and <paramref name="refuseBlockedStep" />, refuses a
    ///     step whose landing cell is off the navmesh (never teleports through a wall) and snaps to ground
    ///     otherwise. Returns true when the step reached the destination exactly.
    /// </summary>
    private static bool StepToward(MonsterEntity monster, float destX, float destZ, float speed, float dt,
        ZoneGeometry? geometry, bool refuseBlockedStep)
    {
        var dx = destX - monster.PosX;
        var dz = destZ - monster.PosZ;
        var distance = MathF.Sqrt(dx * dx + dz * dz);
        if (distance <= 0.0001f)
            return true;

        var step = speed * dt;
        if (step <= 0f)
            return false;

        float newX, newZ;
        bool arrived;
        if (step >= distance)
        {
            newX = destX;
            newZ = destZ;
            arrived = true;
        }
        else
        {
            newX = monster.PosX + dx / distance * step;
            newZ = monster.PosZ + dz / distance * step;
            arrived = false;
        }

        if (geometry is { } g)
        {
            if (refuseBlockedStep && !g.IsWalkable(newX, newZ))
                return false; // blocked -- refuse the step (no pathfinding fallback), keep current position

            if (g.TryGetGroundHeight(newX, newZ, out var groundY))
                monster.PosY = groundY;
        }

        monster.PosX = newX;
        monster.PosZ = newZ;
        monster.Heading = MathF.Atan2(dx, dz);
        return arrived;
    }

    private static void CommitPosition(MonsterEntity monster, ZoneGeometry geometry, float x, float z)
    {
        monster.PosX = x;
        monster.PosZ = z;
        if (geometry.TryGetGroundHeight(x, z, out var groundY))
            monster.PosY = groundY;
    }

    /// <summary>True once the goal has relocated far enough from the cached route's goal to warrant a re-plan.</summary>
    private static bool PathGoalMoved(MonsterEntity monster, float targetX, float targetZ)
    {
        var dx = targetX - monster.PathGoalX;
        var dz = targetZ - monster.PathGoalZ;
        return dx * dx + dz * dz > PathReplanGoalMoveThreshold * PathReplanGoalMoveThreshold;
    }

    /// <summary>
    ///     True if the immediate step toward the current cached waypoint would land off the navmesh -- a signal
    ///     to re-plan. False when there is no current waypoint (exhaustion already forces a re-plan). Funnel
    ///     waypoints are on-mesh, so for a fresh route this is effectively always false; it only fires when a
    ///     sub-threshold goal drift has left the tail of a stale route clipping an obstacle.
    /// </summary>
    private static bool NextStepBlocked(MonsterEntity monster, ZoneGeometry geometry, float speed, float dt)
    {
        if (monster.WaypointCursor >= monster.PathWaypoints.Count)
            return false;

        var waypoint = monster.PathWaypoints[monster.WaypointCursor];
        var dx = waypoint.X - monster.PosX;
        var dz = waypoint.Y - monster.PosZ;
        var distance = MathF.Sqrt(dx * dx + dz * dz);
        if (distance <= ArrivalEpsilon)
            return false;

        var step = MathF.Min(speed * dt, distance);
        if (step <= 0f)
            return false;

        var newX = monster.PosX + dx / distance * step;
        var newZ = monster.PosZ + dz / distance * step;
        return !geometry.IsWalkable(newX, newZ);
    }

    private static float DistanceSquared(float x1, float z1, float x2, float z2)
    {
        var dx = x1 - x2;
        var dz = z1 - z2;
        return dx * dx + dz * dz;
    }
}
