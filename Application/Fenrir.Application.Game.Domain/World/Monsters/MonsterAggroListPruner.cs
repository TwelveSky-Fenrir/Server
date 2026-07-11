namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     <c>AdjustValidAttackTarget</c> (<c>Server/ts25zone/S07_MyGame05.cpp:336-447</c>): the per-tick prune of a
///     monster's own aggro list -- the SAME shared 50-slot attacker table <see cref="MonsterEntity.RegisterAttackDamage" />
///     / <see cref="MonsterEntity.RegisterAcquisition" /> write into (legacy <c>mAttackDamage[]</c>,
///     <c>Server/ts25zone/H07_MyGame.h:1117-1120</c>, capacity <c>MAX_MONSTER_OBJECT_ATTACK_NUM</c> ==
///     <c>Server/Header/Protocol/DEFINE.h:603</c> -- see <see cref="MonsterAttackDamageEntry" />'s own citation
///     trail for that shared-table identity). Behavior contract <c>A3-aggro-pruning</c>.
/// </summary>
/// <remarks>
///     Deliberately a pure computation over a snapshot (<see cref="MonsterEntity.SnapshotAttackDamage" />), not a
///     mutator of <see cref="MonsterEntity" />'s live table itself -- <see cref="MonsterEntity.ReplaceAttackDamage" />
///     is the in-place "replace the attacker table with these survivors" write-back this pass needs.
///     <para>
///         UPDATE (2026-07-11, recovered <c>monster-ai-recipe-bodies</c> behavior contract): the call site this
///         type's own remarks used to flag as missing is now wired --
///         <see cref="MonsterAiSystem" />'s Standard-recipe decision tick (its private
///         <c>RunPrunedAttackerEngagement</c>) calls <see cref="Prune" /> then
///         <see cref="MonsterEntity.ReplaceAttackDamage" /> unconditionally once reached, and the
///         <see cref="Result.HasValidAttackers" /> true/false branch the previous remark flagged as unresolved
///         is now grounded: false means no transition at all this tick (<c>S07_MyGame05.cpp:1071-1074</c>'s
///         post-prune empty-table bail); true feeds the recovered survivor-selection recipe (per-entry
///         melee-range coin flip, uniform fallback, melee/chase/walk-home branch) -- see that method's own
///         remarks for the full citation trail. <see cref="MonsterAiSystem.RunChase" />'s existing
///         single-locked-target prune (its own <c>AdjustValidAttackTarget</c> citation) remains a narrower,
///         separately-wired predecessor of this fuller, whole-table mechanism -- the two are still not unified
///         (an open question the recovered contract explicitly left unresolved, not this session's to decide).
///     </para>
/// </remarks>
public static class MonsterAggroListPruner
{
    /// <summary>
    ///     One surviving aggro-list entry after a prune pass, carrying everything a future in-place table
    ///     replacement needs to reconstruct a <see cref="MonsterAttackDamageEntry" /> without a second lookup
    ///     pass. <see cref="DistanceSquared" /> is always freshly recomputed this pass (side effect 2 of the
    ///     contract: "this happens for every survivor, every time, not only when the previous value was missing
    ///     or stale") -- planar (X/Z) squared distance only, vertical separation excluded
    ///     (<c>Server/Header/mapcheck.h:27-29</c>), matching every other distance check in
    ///     <see cref="MonsterAiSystem" />.
    /// </summary>
    /// <param name="CharacterId">Identity half of the legacy identity+session slot key.</param>
    /// <param name="SessionToken">
    ///     Session half of the slot key -- the attacker's live <see cref="PlayerRuntimeState" /> reference
    ///     captured when this entry was originally recorded, re-checked by reference every pass (see
    ///     <see cref="MonsterAttackDamageEntry" />'s own remarks for why a reference re-check already captures
    ///     legacy's separate "slot still occupied" + "same unique identifier" checks as one comparison).
    /// </param>
    /// <param name="CumulativeDamage">Carried forward completely unchanged -- this behavior never modifies damage totals.</param>
    /// <param name="DistanceSquared">Freshly recomputed planar squared distance from the monster to this attacker.</param>
    public readonly record struct Survivor(int CharacterId, object SessionToken, long CumulativeDamage,
        float DistanceSquared);

    /// <summary>
    ///     Outcome of one <see cref="Prune" /> pass. <see cref="HasValidAttackers" /> is the flag legacy's own
    ///     caller reads immediately after this behavior returns to decide whether the monster takes any
    ///     attack/chase action at all this tick (<c>S07_MyGame05.cpp:1070-1074</c>) -- true only when at least
    ///     one entry survived.
    /// </summary>
    public readonly record struct Result(IReadOnlyList<Survivor> Survivors)
    {
        public bool HasValidAttackers => Survivors.Count > 0;
    }

    /// <summary>
    ///     Runs one <c>AdjustValidAttackTarget</c> pass over <paramref name="monster" />'s own aggro list.
    /// </summary>
    /// <param name="zone">
    ///     Resolves each tracked attacker's live <see cref="PlayerRuntimeState" /> -- exclusions 1-5 of the
    ///     gauntlet below all key off this lookup.
    /// </param>
    /// <param name="monster">The monster whose own aggro list (attacker table) is being pruned.</param>
    /// <param name="allMonsters">
    ///     The population the mid-range crowd-control check scans for other pursuers of the same candidate
    ///     (<see cref="MonsterAiSystem.TryAcquireTarget" />'s own anti-clump check, <c>CountOtherPursuers</c>, is
    ///     this same idiom already scoped to one zone's own monster population -- see
    ///     <see cref="CountOtherPursuers" />'s own remarks for why that zone-local scope is a faithful narrowing
    ///     of the contract's shard-wide framing, not an approximation of different behavior). Typed as
    ///     <see cref="IEnumerable{T}" /> (not <see cref="IReadOnlyList{T}" />) specifically so
    ///     <see cref="MonsterAiSystem" /> can pass <see cref="Zone.MonstersSnapshot" /> straight through --
    ///     nothing here ever needs indexed access, only a single forward scan (<see cref="CountOtherPursuers" />).
    /// </param>
    /// <param name="resultBuffer">
    ///     Optional reused scratch list -- cleared and refilled in place, avoiding a fresh allocation on a hot
    ///     per-monster-per-AI-tick call path. When supplied, <see cref="Result.Survivors" /> IS this same list
    ///     (valid only until the caller's next <see cref="Prune" /> call reusing it); when omitted, a fresh list
    ///     is allocated and safe to keep indefinitely.
    /// </param>
    public static Result Prune(Zone zone, MonsterEntity monster, IEnumerable<MonsterEntity> allMonsters,
        List<Survivor>? resultBuffer = null)
    {
        var survivors = resultBuffer ?? [];
        survivors.Clear();

        var meleeRadius = monster.Template.RadiusInfo1;
        var leashRadius = monster.Template.RadiusInfo2;

        // Whole-behavior bypass (S07_MyGame05.cpp:351-352,433-437): a non-positive melee OR leash/detection
        // radius (monster-data misconfiguration) skips ALL per-entry evaluation and wipes the list
        // unconditionally, every time this runs -- such a monster can never sustain an aggro list past the
        // first pruning pass it reaches, no matter how many valid attackers it had.
        if (meleeRadius <= 0 || leashRadius <= 0)
            return new Result(survivors);

        var meleeRadiusSq = (float)meleeRadius * meleeRadius;
        var leashRadiusSq = (float)leashRadius * leashRadius;

        // Iterated (and therefore survivor-appended) in the snapshot's own oldest-to-newest order
        // (MonsterEntity.SnapshotAttackDamage's own remarks) -- preserves "surviving entries only, in their
        // original relative order, with no gaps" with no extra bookkeeping.
        foreach (var entry in monster.SnapshotAttackDamage())
        {
            if (!TryEvaluateEntry(zone, monster, allMonsters, entry, meleeRadiusSq, leashRadiusSq,
                    out var distanceSquared))
                continue;

            survivors.Add(new Survivor(entry.CharacterId, entry.SessionToken, entry.CumulativeDamage,
                distanceSquared));
        }

        return new Result(survivors);
    }

    /// <summary>
    ///     The per-entry exclusion gauntlet, evaluated in the contract's exact order, stopping at the first
    ///     match (S07_MyGame05.cpp:358-423). Entries 6 (pre-spawn/unrecovered second action-state category) and
    ///     7 (coarse 3D grid prefilter, including vertical separation) are NOT modeled -- no cataloged Fenrir
    ///     equivalent exists for either (matching <see cref="MonsterAiSystem.IsCandidateValid" />'s own
    ///     already-documented action-state gap, and see this type's own behavior-contract open questions for the
    ///     grid gap -- the contract itself could not recover the grid-cell-size value needed to reproduce it).
    /// </summary>
    private static bool TryEvaluateEntry(Zone zone, MonsterEntity monster, IEnumerable<MonsterEntity> allMonsters,
        MonsterAttackDamageEntry entry, float meleeRadiusSq, float leashRadiusSq, out float distanceSquared)
    {
        distanceSquared = 0f;

        // Exclusions 1+2 combined (:358-365): the tracked slot must still resolve to a live, currently-occupying
        // character whose own live reference is the SAME one captured when this entry was recorded.
        if (!zone.TryGetPlayer(entry.CharacterId, out var player) || player is null ||
            !ReferenceEquals(player, entry.SessionToken))
            return false;

        // Exclusion 3 (:366-369, mid-cross-shard-transfer), exclusion 4 (:370-373, hidden/invisible -- always
        // false today, no Fenrir stealth state, matching MonsterAiSystem.IsHiding's own named-always-false
        // precedent), exclusion 5 (:374-377, dead).
        if (player.IsMovingZone || IsHiding(player) || player.IsDead)
            return false;

        distanceSquared = DistanceSquared(monster.PosX, monster.PosZ, player.PosX, player.PosZ);

        if (distanceSquared > leashRadiusSq)
            return false; // beyond leash/detection radius: dropped outright (:388-391)

        if (distanceSquared > meleeRadiusSq)
        {
            // Mid-range (melee < d <= leash): crowd-control cap (:394-416). Full removal from consideration for
            // this tick, not merely a lower priority at final target-selection time.
            var otherPursuers = CountOtherPursuers(allMonsters, monster, entry.CharacterId);
            return otherPursuers < monster.PursuerCapacity;
        }

        // Near-range (d <= melee): height check NOT applied here -- which MonsterRowDto field is the "vertical/
        // height extent" this check reads was not established by the contract (same open gap as
        // MonsterAiSystem.TryShortRangeRetarget's own un-applied body-size height filter; the contract's own
        // wording suggests it is likely the identical legacy field). TODO(A3-aggro-pruning): apply once a
        // legacy-behavior-translator follow-up resolves the field.
        return true;
    }

    /// <summary>
    ///     Legacy <c>IsHiding()</c> stealth predicate -- see <see cref="MonsterAiSystem.IsHiding" />'s own remarks
    ///     (duplicated here, not shared, since that method is private to its own file): <see cref="PlayerRuntimeState" />
    ///     has no stealth/hide state yet, so this is a named, always-<c>false</c> predicate rather than a
    ///     fabricated check -- no entry is ever excluded on this ground today. Kept as its own method (not
    ///     inlined) so the gauntlet reads as the full legacy ordering and the gap is greppable when stealth is
    ///     added.
    /// </summary>
    private static bool IsHiding(PlayerRuntimeState player)
    {
        _ = player;
        return false;
    }

    /// <summary>
    ///     Crowd-control pursuer count for the mid-range band (S07_MyGame05.cpp:394-416): every OTHER live
    ///     monster already locked onto <paramref name="candidateCharacterId" /> while chasing or winding up an
    ///     attack. Deliberately zone-scoped (see <see cref="Prune" />'s own <paramref name="allMonsters" /> doc)
    ///     rather than the contract's literal shard-wide framing (<c>Server/ts25zone/H01_MainApplication.h:76</c>)
    ///     -- mirrors <see cref="MonsterAiSystem" />'s own <c>CountOtherPursuers</c> anti-clump check exactly
    ///     (same counting rule, same states-count-as-"attacking" set), duplicated here rather than shared since
    ///     that method is private to its own file.
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

    /// <summary>Planar (X/Z only) squared distance -- vertical separation excluded, matching <c>Server/Header/mapcheck.h:27-29</c>.</summary>
    private static float DistanceSquared(float x1, float z1, float x2, float z2)
    {
        var dx = x1 - x2;
        var dz = z1 - z2;
        return dx * dx + dz * dz;
    }
}
