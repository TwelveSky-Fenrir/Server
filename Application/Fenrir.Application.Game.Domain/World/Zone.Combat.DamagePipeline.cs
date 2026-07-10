using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     B15 combat depth terms shared by both cross-avatar attack paths -- <c>mCase</c> 2 (enemy tribe,
///     <c>Zone.Combat.cs</c>'s <see cref="ApplyCombatCommand" />) and <c>mCase</c> 1 (duel, <c>Zone.Duel.cs</c>'s
///     <see cref="ApplyDuelAttack" />). Runs on the tick thread only (single writer), allocation-free on the hot
///     path (no shield/no reflect short-circuits before any work). See the B15 damage-pipeline behavior contract
///     (<c>Server/ts25zone/S07_MyGame02.cpp:1267-1359</c>, <c>S07_MyGame04.cpp:2614-2749</c>).
/// </summary>
public sealed partial class Zone
{
    /// <summary>
    ///     Zone124 (the duel-arena server type) disables reflect entirely
    ///     (<c>S07_MyGame02.cpp:1277</c>). In Fenrir's one-map-per-Zone model this is <see cref="MapId" /> == 124.
    /// </summary>
    private const short ReflectDisabledZoneId = 124;

    /// <summary>
    ///     The pre-main destroyer roll + reflect step for a cross-avatar hit (destroyer first, then reflect --
    ///     <c>S07_MyGame02.cpp:1267-1334</c>). On a successful destroyer roll the defender's Holy-Shields are
    ///     hard-cleared. When reflect fires, the ATTACKER takes 150% of the finalized pre-element main damage
    ///     (and dies + credits the defender if that is lethal), the defender takes no damage this hit, and this
    ///     returns <c>true</c> so the caller aborts the rest of the hit. Must run BEFORE
    ///     <see cref="ApplyHolyShieldAbsorption" /> so a destroyed shield can no longer absorb.
    /// </summary>
    private bool TryApplyReflectAndDestroyer(PlayerRuntimeState attackerState, PlayerRuntimeState defenderState,
        in AttackOutcome outcome, CrossAvatarAttackKind kind)
    {
        // ViewDamage is post-element/pre-life-cap; subtracting ElementDamage recovers the finalized pre-shield,
        // pre-element main damage the destroyer/reflect step is defined against (S07_MyGame02.cpp:1267).
        var preElementMainDamage = outcome.ViewDamage - outcome.ElementDamage;

        var reflect = ReflectResolver.Resolve(
            attackerState.Buffs.Buff[ReflectResolver.DestroyerBuffSlot * 2],
            defenderState.Buffs.Buff[ReflectResolver.ReflectBuffSlot * 2],
            attackerState.Level,
            defenderState.Level,
            0, // TODO(world-state): no anti-reflect stat on EffectiveStats yet -- see ReflectResolver's remarks.
            MapId != ReflectDisabledZoneId,
            preElementMainDamage,
            _random);

        if (reflect.DestroyerSucceeded)
            RemoveDefenderHolyShields(defenderState);

        if (!reflect.ReflectFired)
            return false;

        attackerState.Life -= reflect.ReflectDamage;
        attackerState.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        if (attackerState.Life <= 0)
            switch (kind)
            {
                // Enemy reflect-kill (death-reason 3): the defender is credited as the killer through the same
                // ClampGrant/cooldown-bounded pipeline a normal enemy kill uses. Fenrir does not model the
                // legacy's crit-vs-normal kill-credit-type marker, so the "critical-hit type" nuance the
                // contract notes for a reflect kill is a pre-existing gap, not introduced here.
                case CrossAvatarAttackKind.EnemyTribe:
                    ApplyPvpKillRewards(defenderState, attackerState);
                    ApplyDeath(attackerState.CharacterId, DeathCause.PlayerKill);
                    break;
                // Duel reflect-kill (death-reason 1): no reward pipeline, matching every other duel kill in
                // Fenrir (see Zone.Duel.cs's own remarks on why a duel kill is deliberately reward-free).
                case CrossAvatarAttackKind.Duel:
                    ApplyDeath(attackerState.CharacterId, DeathCause.Duel);
                    break;
            }

        return true;
    }

    /// <summary>
    ///     Applies base-slot Holy-Shield absorption (player source) to a cross-avatar hit and returns the
    ///     view/real damage the defender should observe. Absorption reduces the pre-element main damage
    ///     (<c>S07_MyGame02.cpp:1338</c>, before the element add at <c>:1340</c>); the element is then re-added
    ///     and the result re-clamped to the defender's remaining life. When nothing is absorbed the returned
    ///     values equal the resolver's own <see cref="AttackOutcome.ViewDamage" />/
    ///     <see cref="AttackOutcome.DamageApplied" /> exactly.
    /// </summary>
    private (int View, int Real) ApplyHolyShieldAbsorption(PlayerRuntimeState defenderState, in AttackOutcome outcome)
    {
        var preElementMainDamage = outcome.ViewDamage - outcome.ElementDamage;
        var absorbed = HolyShieldResolver.Absorb(defenderState.Buffs.Buff, preElementMainDamage);

        if (absorbed > 0)
            BroadcastHolyShieldChange(defenderState);

        var view = preElementMainDamage - absorbed + outcome.ElementDamage;
        var real = Math.Min(view, defenderState.Life);
        return (view, real);
    }

    private void RemoveDefenderHolyShields(PlayerRuntimeState defenderState)
    {
        if (HolyShieldResolver.RemoveAll(defenderState.Buffs.Buff))
            BroadcastHolyShieldChange(defenderState);
    }

    /// <summary>
    ///     Notifies the defender + AOI neighbors of a Holy-Shield slot change. Legacy sends a targeted,
    ///     sort-coded broadcast (3 cleared / 5 player-absorb / 6 monster-absorb, shifted to "tier position + 2"
    ///     for a tiered slot); Fenrir collapses that onto the existing buff-array change broadcast
    ///     (<see cref="RecomputeStatsAndBroadcastBuffs" />), the one already-wired path for a buff-slot change --
    ///     the sort-coded variant is a wire-fidelity gap, not a gameplay one (the shield's absorbable value on
    ///     the array IS updated). Slot 9 does not feed <see cref="Stats.StatCalculator" />, so the recompute is a
    ///     no-op on stats and only refreshes the wire view.
    /// </summary>
    private void BroadcastHolyShieldChange(PlayerRuntimeState state)
    {
        var changed = state.BuffChangeScratch;
        Array.Clear(changed);
        changed[HolyShieldResolver.BaseSlot] = 1;
        foreach (var slot in HolyShieldResolver.TieredSlots)
            changed[slot] = 1;

        RecomputeStatsAndBroadcastBuffs(state, changed);
    }

    /// <summary>Which cross-avatar attack path is applying the shared depth terms -- decides reflect-kill crediting.</summary>
    private enum CrossAvatarAttackKind
    {
        EnemyTribe,
        Duel
    }
}
