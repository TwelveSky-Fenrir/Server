using Fenrir.Application.Game.Simulation;

namespace Fenrir.Application.Game.World.Monsters;

/// <summary>
///     Per-tick monster AI (report ServerDocs/30_Fenrir_ServerLogic/05_game_mechanics.md §3, verified against
///     <c>Server/ts25zone/S07_MyGame05.cpp</c>'s <c>MONSTER_OBJECT::Update</c>): a SIMPLIFIED FSM covering
///     spawn-wait, proximity aggro detection, pursuit with a spawn-anchored leash, a windup-timed attack
///     state, and a forced return-to-spawn -- the "simple FSM is fine for this pass" scope this task's brief
///     explicitly allows. One instance per <see cref="Zone" />.
/// </summary>
/// <remarks>
///     Deliberately NOT ported (open issues, not oversights):
///     <list type="bullet">
///         <item>
///             Random wander while idle (legacy: "patrouille aléatoire") -- no wander radius/timing constant was
///             found in the source within this task's scope, so an idle monster simply stays at its home point
///             instead of inventing one; it still walks home if displaced (<see cref="MonsterAiState.Patrol" />).
///         </item>
///         <item>
///             The leash bound reuses the monster's OWN spawn region <c>Radius</c> (
///             <see cref="MonsterEntity.LeashRadius" />)
///             -- the one concrete <c>PathForMonsterAttack</c> call site read during this investigation actually
///             bounds pursuit against the TARGET's position with <c>mRadiusInfo[0]</c> (closing to attack range), not
///             a distance-from-spawn leash; no hardcoded "leash distance from home" constant was located in the
///             source. Using the region's own configured scatter radius as the leash is a documented, reasonable,
///             data-driven stand-in, not a verified constant.
///         </item>
///         <item>
///             Monster-initiated damage to a player (legacy: <c>ProcessAttack04</c> -- NEVER reached via the wire
///             dispatch in practice, only ever called directly from the monster's own AI, <c>S07_MyGame05.cpp:3961</c>)
///             fires once per attack-windup entry via <see cref="Zone.ResolveMonsterAttack" />
///             (<see cref="Combat.MonsterCombatResolver.ResolveMvpAttack" /> -- verified formula, see that type's own
///             remarks for exactly what is/isn't reproduced).
///         </item>
///         <item>
///             Boss/guard/tribe-symbol special AI (aSort 7/8/12 in the full legacy table) -- no such content in this
///             batch.
///         </item>
///     </list>
/// </remarks>
public sealed class MonsterAiSystem : ISimulationSystem
{
    /// <summary>One legacy tick's worth of movement time, matching the report's own "vitesse × dTime" with dTime ≈ 0.5 s.</summary>
    private const float TickSeconds = SimulationClock.LegacyTickMilliseconds / 1000f;

    /// <summary>Close enough to "arrived" that jitter/overshoot never leaves a monster oscillating around its destination.</summary>
    private const float ArrivalEpsilon = 1f;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var dt = TickSeconds * legacyTicksElapsed;

        foreach (var monster in zone.MonstersSnapshot)
            Update(zone, monster, dt);
    }

    private void Update(Zone zone, MonsterEntity monster, float dt)
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
                RunDecision(zone, monster);
                break;

            case MonsterAiState.Patrol:
                MoveToward(zone, monster, monster.HomeX, monster.HomeZ, monster.Template.WalkSpeed, dt);
                if (DistanceSquared(monster.PosX, monster.PosZ, monster.HomeX, monster.HomeZ) <=
                    ArrivalEpsilon * ArrivalEpsilon)
                {
                    monster.AiState = MonsterAiState.Decision;
                    monster.StateTicks = 0;
                }
                else
                {
                    TryAcquireTarget(zone,
                        monster); // re-checked every tick, same as Decision -- a wandering monster can still be aggroed
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

            case MonsterAiState.ReturnToSpawn:
                MoveToward(zone, monster, monster.HomeX, monster.HomeZ, monster.Template.RunSpeed, dt);
                if (DistanceSquared(monster.PosX, monster.PosZ, monster.HomeX, monster.HomeZ) <=
                    ArrivalEpsilon * ArrivalEpsilon)
                {
                    monster.TargetCharacterId = null;
                    monster.AiState = MonsterAiState.Spawning; // report 05 §3: aSort 19 -> teleport home -> aSort 0
                    monster.StateTicks = 0;
                }

                break;

            case MonsterAiState.Dead:
                break; // transient -- removed from the pool before the next tick drains it
        }
    }

    private void RunDecision(Zone zone, MonsterEntity monster)
    {
        if (TryAcquireTarget(zone, monster))
            return;

        if (DistanceSquared(monster.PosX, monster.PosZ, monster.HomeX, monster.HomeZ) >
            ArrivalEpsilon * ArrivalEpsilon)
            monster.AiState = MonsterAiState.Patrol;
        // else: stays Decision/idle at home -- see class remarks on random wander not being ported.
    }

    /// <summary>
    ///     <c>SelectAvatarIndexForPossibleAttack</c> (<c>S07_MyGame05.cpp:113-216</c>): proactive aggro is gated
    ///     to <c>mAttackType ∈ {1,3,6}</c> (l.123 -- a prior pass here had NO such gate, so passive/reactive-only
    ///     monster types incorrectly hunted players on sight) and detection radius is <c>mRadiusInfo[1]</c>
    ///     (l.132/182 -- i.e. <see cref="Fenrir.Data.World.MonsterRowDto.RadiusInfo2" />, NOT
    ///     <see cref="Fenrir.Data.World.MonsterRowDto.RadiusInfo1" />, which is the SMALLER melee-range radius
    ///     <see cref="RunChase" /> uses to decide when to transition into <see cref="MonsterAiState.AttackWindup" />
    ///     -- a prior pass here had the two radii swapped between these two call sites).
    /// </summary>
    private bool TryAcquireTarget(Zone zone, MonsterEntity monster)
    {
        if (monster.Template.AttackType is not (1 or 3 or 6))
            return false;

        var detectionRadius = monster.Template.RadiusInfo2;
        var detectionRadiusSq = (float)detectionRadius * detectionRadius;

        foreach (var characterId in zone.NeighborsOfPosition(monster.PosX, monster.PosZ))
        {
            if (!zone.TryGetPlayer(characterId, out var player) || player is null || player.IsDead)
                continue;

            if (DistanceSquared(monster.PosX, monster.PosZ, player.PosX, player.PosZ) > detectionRadiusSq)
                continue;

            monster.TargetCharacterId = characterId;
            RecordAggro(monster, characterId);
            monster.AiState = MonsterAiState.Chase;
            monster.StateTicks = 0;
            return true;
        }

        return false;
    }

    /// <summary>Bounded FIFO, oldest purged first (report 05 §3: <c>MAX_MONSTER_OBJECT_ATTACK_NUM = 50</c>).</summary>
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
            monster.TargetCharacterId = null;
            monster.AiState = MonsterAiState.ReturnToSpawn;
            monster.StateTicks = 0;
            return;
        }

        // Leash: once pursuit would carry the monster further from home than its region's own radius, give up
        // and head back instead of closing the remaining distance (see class remarks on this being a
        // documented stand-in, not a verified constant).
        if (DistanceSquared(monster.PosX, monster.PosZ, monster.HomeX, monster.HomeZ) >
            monster.LeashRadius * monster.LeashRadius)
        {
            monster.TargetCharacterId = null;
            monster.AiState = MonsterAiState.ReturnToSpawn;
            monster.StateTicks = 0;
            return;
        }

        // A005's own "close enough to transition into an attack" threshold uses mRadiusInfo[0] (RadiusInfo1,
        // the SMALLER melee-range radius, S07_MyGame05.cpp:1393) -- distinct from ProcessAttack04's own, more
        // lenient mRadiusInfo[1] (RadiusInfo2) validation at actual attack-execution time
        // (MonsterCombatResolver.ResolveMvpAttack's own remarks), which tolerates a bit of target movement
        // during the windup delay. A prior pass here used RadiusInfo2, swapped with TryAcquireTarget's own bug.
        var attackRadiusSq = (float)monster.Template.RadiusInfo1 * monster.Template.RadiusInfo1;
        if (DistanceSquared(monster.PosX, monster.PosZ, target.PosX, target.PosZ) <= attackRadiusSq)
        {
            monster.AiState = MonsterAiState.AttackWindup;
            monster.StateTicks = 0;
            return;
        }

        MoveToward(zone, monster, target.PosX, target.PosZ, monster.Template.RunSpeed, dt);
    }

    /// <summary>
    ///     Straight-line step of <c>speed * dt</c> toward (targetX, targetZ). Validated against
    ///     <see cref="Zone.Geometry" /> when loaded (walkability of the PROPOSED point, height snapped to
    ///     terrain); when no <c>.WM</c> is loaded, the step is applied unconditionally -- the SAME documented
    ///     M1 placeholder posture <see cref="Movement.MovementRules" /> already uses for players.
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
