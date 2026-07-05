using Fenrir.Application.Game.Simulation;

namespace Fenrir.Application.Game.World.Monsters;

/// <summary>
///     Per-tick monster AI (<c>Server/ts25zone/S07_MyGame05.cpp</c>'s <c>MONSTER_OBJECT::Update</c>): a
///     simplified FSM covering spawn-wait, proximity aggro detection, pursuit with a spawn-anchored leash, a
///     windup-timed attack state (melee, and a Zone175-boss ranged variant), a hit-stagger state, and forced
///     return-to-spawn. One instance per <see cref="Zone" />.
/// </summary>
/// <remarks>
///     Deliberately not ported:
///     <list type="bullet">
///         <item>
///             Random wander while idle -- no wander radius/timing constant found in source; an idle monster stays at
///             its home point.
///         </item>
///         <item>
///             The leash bound reuses the monster's own spawn-region radius (<see cref="MonsterEntity.LeashRadius" />)
///             -- no "leash distance from home" constant was found in source, so this is a data-driven stand-in.
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
                        monster); // re-checked every tick -- a wandering monster can still be aggroed
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
                MoveToward(zone, monster, monster.HomeX, monster.HomeZ, monster.Template.RunSpeed, dt);
                if (DistanceSquared(monster.PosX, monster.PosZ, monster.HomeX, monster.HomeZ) <=
                    ArrivalEpsilon * ArrivalEpsilon)
                {
                    monster.TargetCharacterId = null;
                    monster.AiState = MonsterAiState.Spawning; // aSort 19 -> teleport home -> aSort 0
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
    ///     <c>SelectAvatarIndexForPossibleAttack</c> (<c>S07_MyGame05.cpp:113-216</c>): proactive aggro gated to
    ///     <c>mAttackType ∈ {1,3,6}</c>; detection radius is <see cref="Fenrir.Data.World.MonsterRowDto.RadiusInfo2" />,
    ///     not <see cref="Fenrir.Data.World.MonsterRowDto.RadiusInfo1" /> (the smaller melee-range radius
    ///     <see cref="RunChase" /> uses for the attack-windup transition) -- do not swap these two.
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
            monster.TargetCharacterId = null;
            monster.AiState = MonsterAiState.ReturnToSpawn;
            monster.StateTicks = 0;
            return;
        }

        // Leash: once pursuit would carry the monster further from home than its region's own radius, give up
        // and head back instead of closing the remaining distance.
        if (DistanceSquared(monster.PosX, monster.PosZ, monster.HomeX, monster.HomeZ) >
            monster.LeashRadius * monster.LeashRadius)
        {
            monster.TargetCharacterId = null;
            monster.AiState = MonsterAiState.ReturnToSpawn;
            monster.StateTicks = 0;
            return;
        }

        var distanceToTargetSq = DistanceSquared(monster.PosX, monster.PosZ, target.PosX, target.PosZ);

        // Attack-windup transition threshold uses RadiusInfo1 (melee range), distinct from the more lenient
        // RadiusInfo2 validation at actual attack-execution time -- do not swap these two.
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
        // since Fenrir's single-locked-target Chase has no equivalent candidate list to roll over.
        if (IsZone175TypeBoss(monster.Template.SpecialType))
        {
            var detectionRadiusSq = (float)monster.Template.RadiusInfo2 * monster.Template.RadiusInfo2;
            if (distanceToTargetSq <= detectionRadiusSq)
            {
                monster.AiState = MonsterAiState.RangedAttackWindup;
                monster.StateTicks = 0;
                return;
            }
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
