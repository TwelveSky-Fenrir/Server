using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     The one-shot state transition a monster's own <see cref="MonsterEntity" /> undergoes at the exact instant
///     an avatar's killing blow lands -- behavior contract <c>wave12/A3-death-sequence.md</c>, side effects 2-3.
/// </summary>
/// <remarks>
///     Réf. C++ : <c>Server/ts25zone/S07_MyGame02.cpp:3129-3138</c> (action-state transition to the
///     corpse-countdown state (legacy <c>aSort</c> 12, <see cref="MonsterAiState.Dead" />), countdown-accumulator
///     reset, and the corpse-faces-its-killer angle computation); <c>Server/Header/mapcheck.h:32-72</c> (the
///     angle formula itself -- reproduced here via the same <c>MathF.Atan2(dx, dz)</c> idiom
///     <see cref="MonsterAiSystem" />'s own movement/pathing code already uses for "face the point being moved
///     toward").
///     <para>
///         Tick-thread-only, same single-writer invariant as every other <see cref="MonsterEntity" /> mutation:
///         every production caller of this method (the avatar-kills-monster combat path,
///         <c>Zone.ApplyPvmAttack</c>) runs entirely on this monster's own zone's tick thread
///         (<c>Zone.ApplyCombatCommand</c>'s own remarks: "entirely on this zone's own tick thread").
///     </para>
///     <para>
///         NOT modeled here (open questions the source contract itself flags as genuinely unrecoverable, not
///         guessed at -- see the contract's own "Open questions" section):
///         <list type="bullet">
///             <item>
///                 The death "special animation" flag roll (contract side effect 4): its gating relies on the
///                 monster's <c>Type</c> classification's own excluded value and on
///                 <c>ATTACK_FOR_PROTOCOL::mAttackActionValue1/2</c>, both of whose concrete trigger values are
///                 redacted in the legacy source itself (struct field comments stripped to placeholder text,
///                 confirmed by direct inspection -- <c>Server/Header/Protocol/STRUCT.h:966-967</c>). No method
///                 for this roll exists here; do not invent the missing thresholds.
///             </item>
///             <item>
///                 The tick-driven half of the mechanic (legacy's own "A013" per-tick countdown routine,
///                 <c>S07_MyGame05.cpp:1532-1656</c>): re-entering the countdown-only state routine every tick
///                 while <see cref="MonsterAiState.Dead" /> remains active, advancing
///                 <see cref="MonsterEntity.StateTicks" />, and deferring the monster's removal from the live
///                 pool (and the respawn-delay clock's own start) until <see cref="IsCorpseCountdownComplete" />
///                 first returns true. Fenrir's existing pipeline (<c>Zone.TryDamageMonster</c>) instead removes
///                 the monster from the live pool and starts the respawn timer immediately at the killing blow,
///                 collapsing the windup into the single broadcast this method feeds -- <see cref="MonsterAiState.Dead" />'s
///                 own remarks already document this as a deliberate, pre-existing Fenrir simplification.
///                 <see cref="IsCorpseCountdownComplete" /> is provided as the pure completion-check primitive a
///                 future wiring pass would need, but nothing in production calls it today -- see this slice's
///                 own wiring manifest for what such a pass would need to touch (<c>MonsterAiSystem.Update</c>'s
///                 <see cref="MonsterAiState.Dead" /> case, and the monster-removal timing in
///                 <c>Zone.TryDamageMonster</c>/<c>MonsterSpawnScheduler.DrainDeaths</c>) -- out of this slice's
///                 own scope since it changes monster lifecycle/removal timing, not just this one state
///                 transition.
///             </item>
///             <item>
///                 The tribe-symbol-stone/alliance-stone-only ownership-resolution broadcast deferred to windup
///                 completion (contract side effect 8): Fenrir's existing <c>MonsterSpawnScheduler.ProcessDeath</c>
///                 already reports tribe-symbol ownership immediately at the killing blow via
///                 <c>ZoneEventBroadcaster.AnnounceSymbolResolved</c>, a pre-existing simplification independent
///                 of this slice -- not touched here.
///             </item>
///         </list>
///     </para>
/// </remarks>
public static class MonsterDeathSequence
{
    /// <summary>
    ///     Applies the corpse-countdown windup transition: computes the knockback vector
    ///     (<see cref="MonsterDeathKnockback.Compute" />), switches <paramref name="monster" /> into
    ///     <see cref="MonsterAiState.Dead" />, resets its countdown accumulator, stores the knockback vector into
    ///     the monster's own repurposed "destination" fields (<see cref="MonsterEntity.TargetLocationX" />/Y/Z --
    ///     Y forced flat, never an absolute world point for the duration of this state), and turns the corpse to
    ///     face its killer.
    /// </summary>
    /// <param name="monster">
    ///     The monster whose life has just reached exactly zero. Mutated in place -- see the class remarks for
    ///     the single-writer/tick-thread requirement.
    /// </param>
    /// <param name="killerX">Killer's world X at the moment of the killing blow.</param>
    /// <param name="killerZ">Killer's world Z at the moment of the killing blow.</param>
    /// <param name="isCriticalHit">Whether the killing blow was itself a critical hit.</param>
    /// <param name="random">Forwarded to <see cref="MonsterDeathKnockback.Compute" /> -- see its own remarks on draw consumption.</param>
    /// <returns>The knockback vector applied, for a caller that wants it for its own telemetry/testing purposes.</returns>
    public static MonsterKnockbackVector BeginCorpseCountdown(MonsterEntity monster, float killerX, float killerZ,
        bool isCriticalHit, IRandomSource random)
    {
        var knockback = MonsterDeathKnockback.Compute(killerX, killerZ, monster.PosX, monster.PosZ,
            monster.Template.DamageType, isCriticalHit, random);

        monster.AiState = MonsterAiState.Dead;
        monster.StateTicks = 0;

        // Repurposed destination field: holds the knockback DIRECTION vector itself for the duration of the
        // countdown state, not an absolute world point (S07_MyGame02.cpp:3132-3134). Vertical forced flat.
        monster.TargetLocationX = knockback.X;
        monster.TargetLocationY = 0f;
        monster.TargetLocationZ = knockback.Z;

        // Corpse faces its killer even though it may simultaneously be flung away from that same killer by the
        // vector above -- same MathF.Atan2(dx, dz) "face the point" idiom MonsterAiSystem's own movement code
        // uses (S07_MyGame02.cpp:3135-3138, Server/Header/mapcheck.h:32-72).
        monster.Heading = MathF.Atan2(killerX - monster.PosX, killerZ - monster.PosZ);

        return knockback;
    }

    /// <summary>
    ///     Whether <paramref name="monster" />'s corpse-countdown windup has run long enough to resolve --
    ///     compares <see cref="MonsterEntity.StateTicks" /> against the per-monster
    ///     <see cref="MonsterRowDto.FrameInfo5" /> threshold (legacy <c>mFrameInfo[4]</c>), the same
    ///     <c>Math.Max(1, (int)monster.Template.FrameInfoN)</c> idiom <c>MonsterAiSystem.Update</c> already uses
    ///     for every other windup-timed <see cref="MonsterAiState" /> case (<c>S07_MyGame05.cpp:1558-1564</c>).
    ///     Pure query -- does not itself advance <see cref="MonsterEntity.StateTicks" /> or mutate anything; see
    ///     this type's own class remarks for why nothing in production calls this yet.
    /// </summary>
    public static bool IsCorpseCountdownComplete(MonsterEntity monster)
    {
        return monster.StateTicks >= Math.Max(1, (int)monster.Template.FrameInfo5);
    }
}
