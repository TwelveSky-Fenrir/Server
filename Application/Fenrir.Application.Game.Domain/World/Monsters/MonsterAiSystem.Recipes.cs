using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     <see cref="MonsterAiSystem" />'s <c>mSpecialSortNumber</c> archetype recipes (behavior contract
///     <c>A3-ai-recipes</c>): the car-thrower/ranged annulus recipe (<see cref="MonsterSpecialSort.CarThrower" />)
///     and the Zone175-type boss multi-candidate acquisition + far/near decision. See the core file
///     (<c>MonsterAiSystem.cs</c>) for the FSM dispatch and the shared candidate gauntlet these consume.
/// </summary>
public sealed partial class MonsterAiSystem
{
    /// <summary>
    ///     Throw-car annulus inner-band ratio (contract's Edge cases -> distance-test semantics): a target
    ///     closer than 25% of the melee radius is rejected -- the thrower cannot lob at point-blank range.
    /// </summary>
    private const float ThrowCarInnerRadiusRatio = 0.25f;

    /// <summary>
    ///     Thrower idle-wander trigger roll span (contract cites a "~1%" per-tick roll,
    ///     <c>S07_MyGame05.cpp:682-688</c>, carried-forward/not-reopened): modeled as a 1-in-<see cref="ThrowerWanderRollSpan" />
    ///     draw. The exact legacy probability basis is NOT in the contract's directly-verified ranges -- this is
    ///     a documented ~1% approximation, flagged for a <c>legacy-behavior-translator</c> follow-up alongside
    ///     the rest of the thrower recipe's ungrounded specifics (its <c>SpecialType -&gt; tribe</c> exclusion map).
    /// </summary>
    private const int ThrowerWanderRollSpan = 100;

    /// <summary>Fixed 50-slot cap on a Zone175 boss's per-tick aggro list (<c>MAX_MONSTER_OBJECT_ATTACK_NUM</c>).</summary>
    private const int Zone175BossAggroCapacity = 50;

    /// <summary>
    ///     Car-thrower / ranged idle recipe (<see cref="MonsterSpecialSort.CarThrower" />,
    ///     <c>S07_MyGame05.cpp:678-799,936-999</c>): first rolls the thrower's own idle-wander trigger; on a
    ///     wander hit it picks a random nearby destination and patrols there (only if the resolved displacement
    ///     clears the 50-unit minimum). If wander did not trigger, it runs the throw-car annulus acquisition and,
    ///     on a hit, commits DIRECTLY to the melee-attack sort (<see cref="MonsterAiState.AttackWindup" />) with
    ///     the target set to the player's location -- so the actual hit resolution routes through the existing
    ///     <see cref="Zone.ResolveMonsterAttack" /> the melee windup already fires, never a separate ranged
    ///     damage path (the contract states ranged damage is client-echoed, not server-resolved).
    /// </summary>
    private void RunThrowerDecision(Zone zone, MonsterEntity monster)
    {
        // Idle-wander trigger: walk-speed-at-least-1 gate plus the ~1% roll (S07_MyGame05.cpp:682-688). The
        // reduced gauntlet / spatial-block details of the trigger's own candidate scan are not modeled -- the
        // trigger here is a pure self-roll, matching the observable "occasionally wander instead of attack."
        if (monster.Template.WalkSpeed >= 1 && _random.NextInt32(ThrowerWanderRollSpan) == 0)
        {
            if (!TryComputeWanderDestination(zone, monster, out var destX, out var destZ))
                return; // resolved displacement fell short of the 50-unit minimum -- discarded, no wander

            monster.WanderTargetX = destX;
            monster.WanderTargetZ = destZ;
            monster.TargetLocationX = destX;
            monster.TargetLocationY = monster.PosY;
            monster.TargetLocationZ = destZ;
            monster.AiState = MonsterAiState.Patrol;
            monster.StateTicks = 0;
            zone.BroadcastMonsterActionChange(monster);
            return;
        }

        // Throw-car annulus acquisition (S07_MyGame05.cpp:719-799). The legacy 100 ms throttle is sub-tick at
        // Fenrir's 500 ms simulation cadence, so it is always satisfied and the acquisition runs every decision
        // tick -- no timer is modeled (documented, not omitted).
        TryThrowCarAcquire(zone, monster);
    }

    /// <summary>
    ///     Throw-car ranged acquisition (<c>S07_MyGame05.cpp:719-799</c>): the file's one true projectile-shaped
    ///     hit-test -- an annulus using true 3D length (<c>GetLengthXYZ</c>, including the vertical axis, with a
    ///     square root), rejecting any candidate closer than <see cref="ThrowCarInnerRadiusRatio" /> of the melee
    ///     radius (<see cref="MonsterRowDto.RadiusInfo1" />) or farther than that radius, then a 50% coin flip,
    ///     first accepted wins. On a hit the monster commits to the melee-attack sort with the target set to the
    ///     player's location. NOT reproduced: the own-tribe/allied-tribe exclusion (<c>:776-779</c>) -- the
    ///     thrower's <c>SpecialType -&gt; tribe</c> map is not in the contract, so no tribe exclusion is applied
    ///     (flagged; every valid in-band candidate is eligible today).
    /// </summary>
    private void TryThrowCarAcquire(Zone zone, MonsterEntity monster)
    {
        if (monster.Template.AttackType is not (1 or 3 or 6))
            return;

        var meleeRadius = monster.Template.RadiusInfo1;
        if (meleeRadius <= 0)
            return; // radius-at-least-1 gate (:735)

        var innerBand = ThrowCarInnerRadiusRatio * meleeRadius;

        foreach (var characterId in zone.NeighborsOfPosition(monster.PosX, monster.PosZ))
        {
            if (!zone.TryGetPlayer(characterId, out var player) || !IsCandidateValid(player))
                continue;

            if (monster.InstanceId is { } requiredInstanceId && player.DungeonInstanceId != requiredInstanceId)
                continue;

            // Annulus band on the true 3D length (un-squared, compared against the raw radius, NOT its square).
            var length = Distance3D(monster, player);
            if (length < innerBand || length > meleeRadius)
                continue;

            // 50% accept, first accepted wins (S07_MyGame05.cpp:208-213 shared coin-flip convention).
            if (_random.NextInt32(2) != 0)
                continue;

            // Direct-to-melee: target = the player's location, then AttackWindup resolves the hit through
            // Zone.ResolveMonsterAttack on its own StateTicks == 1 (S07_MyGame05.cpp:987-996).
            monster.AssignTarget(characterId, player.UniqueNumber, player.PosX, player.PosY, player.PosZ);
            monster.RegisterAcquisition(characterId, player);
            monster.AiState = MonsterAiState.AttackWindup;
            monster.StateTicks = 0;
            zone.BroadcastMonsterActionChange(monster);
            return;
        }
    }

    /// <summary>
    ///     Zone175-type boss idle/decision recipe (<c>A002_FOR_ZONE_175_TYPE_BOSS</c>,
    ///     <c>S07_MyGame05.cpp:218-265,1176-1298</c>): throttled to the same ~1 s window as the standard scan, it
    ///     first rebuilds a multi-candidate aggro list (<see cref="AcquireZone175BossCandidates" />). If nobody
    ///     was recorded it stays idle. Otherwise it branches: with probability one-third it looks for a FAR
    ///     target (recorded distance at or beyond the squared melee radius) at 50% and, if found, commits a
    ///     ranged "cry/signal" attack (<see cref="MonsterAiState.RangedAttackWindup" />, pure-timed -- the
    ///     contract's ranged damage is client-echoed, not server-resolved); with the remaining two-thirds it
    ///     looks for a NEAR target at 50%, falling back to a uniformly random list entry, then commits a melee
    ///     attack if within melee range, else moves toward the target (transitioning to
    ///     <see cref="MonsterAiState.Chase" />).
    /// </summary>
    private void RunZone175BossDecision(Zone zone, MonsterEntity monster, int legacyTicksElapsed)
    {
        // 1 s acquisition throttle (:225-229), same window/reset semantics as the standard scan; reuses the
        // monster's own detection stamp since a given monster is either a boss or a standard mob, never both.
        monster.DetectionThrottleTicks += legacyTicksElapsed;
        if (monster.DetectionThrottleTicks < SimulationClock.MonsterDetectionThrottleLegacyTicks)
            return;

        monster.DetectionThrottleTicks = 0;

        var wideRadius = monster.Template.RadiusInfo2;
        if (wideRadius <= 0)
            return; // radius-at-least-1 gate

        var wideRadiusSq = (float)wideRadius * wideRadius;
        var meleeRadiusSq = (float)monster.Template.RadiusInfo1 * monster.Template.RadiusInfo1;

        var aggro = AcquireZone175BossCandidates(zone, monster, wideRadiusSq);
        if (aggro.Count == 0)
            return; // attack-state flag never set (nobody recorded) -- stay idle (:230-231)

        // 1/3 far (ranged cry) vs 2/3 near (melee/chase) branch (:1197-1297).
        if (_random.NextInt32(3) == 0)
        {
            if (TryPickBossTarget(aggro, meleeRadiusSq, wantFar: true, out var far))
            {
                CommitBossTarget(zone, monster, far, MonsterAiState.RangedAttackWindup);
            }

            // No far target accepted this tick -> take no action (:1229-1230).
            return;
        }

        if (!TryPickBossTarget(aggro, meleeRadiusSq, wantFar: false, out var near))
            near = aggro[_random.NextInt32(aggro.Count)]; // uniformly random fallback entry (:1246-1250)

        // Within melee range -> melee attack; otherwise close in via the generic chase (RunChase moves it
        // toward the committed target at run speed). The legacy "step 100 units, chase only if it moved >= 50"
        // guard is folded into RunChase's own movement here.
        CommitBossTarget(zone, monster, near,
            near.DistanceSquared <= meleeRadiusSq ? MonsterAiState.AttackWindup : MonsterAiState.Chase);
    }

    /// <summary>
    ///     <c>SelectAvatarIndexForPossibleAttackForZone175TypeBoss</c> (<c>S07_MyGame05.cpp:218-265</c>): rebuilds
    ///     the boss's per-tick aggro list from scratch. Loops every neighbor, applies the shared candidate
    ///     gauntlet plus the wide-radius squared-HORIZONTAL range test, and for EACH surviving in-range
    ///     candidate -- independently, at 50%, with NO early stop and NO anti-clump/height filter (the sole
    ///     acceptance rule that differs from every other selection function) -- records it into the fixed
    ///     50-slot aggro scratch and zero-seeds its slot in the shared attacker table via
    ///     <see cref="MonsterEntity.RegisterAcquisition" /> (the same shared <c>SetAttackInfoWithAvatar</c> array
    ///     kill-credit reads). The recorded distance is squared-horizontal, matching the far/near re-test.
    /// </summary>
    /// <remarks>
    ///     Fenrir's shared attacker table accumulates across the fight (it also backs real-damage kill-credit)
    ///     rather than being wiped per acquisition tick as legacy's own list-rebuild implies -- re-acquiring an
    ///     already-tracked candidate is a zero-add no-op (see <see cref="MonsterEntity.RegisterAcquisition" />),
    ///     so the two coexist exactly as they already do for the standard single-target acquisition. Only the
    ///     transient far/near selection list (the returned scratch) is genuinely rebuilt each tick.
    /// </remarks>
    private List<MonsterAggroCandidate> AcquireZone175BossCandidates(Zone zone, MonsterEntity monster,
        float wideRadiusSq)
    {
        var aggro = zone.BorrowBossAggroScratch();

        foreach (var characterId in zone.NeighborsOfPosition(monster.PosX, monster.PosZ))
        {
            if (!zone.TryGetPlayer(characterId, out var player) || !IsCandidateValid(player))
                continue;

            if (monster.InstanceId is { } requiredInstanceId && player.DungeonInstanceId != requiredInstanceId)
                continue;

            var distanceSq = DistanceSquared(monster.PosX, monster.PosZ, player.PosX, player.PosZ);
            if (distanceSq > wideRadiusSq)
                continue;

            // 50% per candidate, NO break -- admits a random ~half of all in-range players (:259-263).
            if (_random.NextInt32(2) != 0)
                continue;

            monster.RegisterAcquisition(characterId, player);
            aggro.Add(new MonsterAggroCandidate(characterId, player.UniqueNumber, distanceSq, player.PosX,
                player.PosY, player.PosZ));

            if (aggro.Count >= Zone175BossAggroCapacity)
                break; // fixed 50-slot list full
        }

        return aggro;
    }

    /// <summary>
    ///     Scans the rebuilt boss aggro list for the first far (<paramref name="wantFar" /> true: recorded
    ///     distance at or beyond the squared melee radius) or near (at or within it) candidate that also wins a
    ///     50% coin flip, matching the standard "50% accept, stop at first accepted" convention.
    /// </summary>
    private bool TryPickBossTarget(List<MonsterAggroCandidate> aggro, float meleeRadiusSq, bool wantFar,
        out MonsterAggroCandidate picked)
    {
        foreach (var candidate in aggro)
        {
            var matches = wantFar
                ? candidate.DistanceSquared >= meleeRadiusSq
                : candidate.DistanceSquared <= meleeRadiusSq;
            if (!matches)
                continue;

            if (_random.NextInt32(2) != 0)
                continue;

            picked = candidate;
            return true;
        }

        picked = default;
        return false;
    }

    /// <summary>
    ///     Commits a chosen boss aggro entry as the monster's locked target (descriptor + broadcast location) and
    ///     transitions to <paramref name="nextState" />, broadcasting the action change.
    /// </summary>
    private static void CommitBossTarget(Zone zone, MonsterEntity monster, MonsterAggroCandidate target,
        MonsterAiState nextState)
    {
        monster.AssignTarget(target.CharacterId, target.UniqueNumber, target.PosX, target.PosY, target.PosZ);
        monster.AiState = nextState;
        monster.StateTicks = 0;
        zone.BroadcastMonsterActionChange(monster);
    }

    /// <summary>
    ///     True 3D length between a monster and a player (<c>GetLengthXYZ</c>, <c>Server/Header/mapcheck.h:12-15</c>):
    ///     includes the vertical axis and takes a square root -- the throw-car annulus's own distance basis,
    ///     unlike every other selection function's squared-horizontal test.
    /// </summary>
    private static float Distance3D(MonsterEntity monster, PlayerRuntimeState player)
    {
        var dx = monster.PosX - player.PosX;
        var dy = monster.PosY - player.PosY;
        var dz = monster.PosZ - player.PosZ;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
