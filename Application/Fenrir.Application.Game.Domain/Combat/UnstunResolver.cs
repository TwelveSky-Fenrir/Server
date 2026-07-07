using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Why a cure attempt (<c>mCase</c> 6, <c>ProcessAttack06</c>) was refused before rolling -- each is a silent,
///     packet-less <c>return;</c> in the legacy.
/// </summary>
public enum UnstunRejectReason
{
    None,
    CurerDead,
    TargetDead,
    TargetShopOpen,

    /// <summary>A cure attempt against a target that is not actually stunned is a silent no-op (:3762-3777).</summary>
    TargetNotStunned,

    /// <summary>
    ///     The target is mid a CROSS-shard zone transfer (<c>World.PlayerRuntimeState.IsMovingZone</c>, surfaced
    ///     via <see cref="CombatantSnapshot.IsMovingZone" /> on <see cref="UnstunAttemptRequest.Target" />) --
    ///     the curer's own such state is never checked, matching legacy exactly (:3758-3773). See this type's
    ///     own remarks and the zone-transfer-in-progress-gate behavior contract (2026-07 round).
    /// </summary>
    TargetMovingZone,

    /// <summary>
    ///     Curer's tribe must equal the target's (alliance not modeled) -- unconditional, no duel exception
    ///     (:3778-3781).
    /// </summary>
    NotAuthorized,

    /// <summary>Only ever produced when the curer's own echo-verification flag is set (:3783-3798).</summary>
    AntiCheatEchoMismatch,

    /// <summary>
    ///     The used skill id is not one of <see cref="UnstunResolver.StunResistSkillIds" />, only checked when the echo
    ///     flag is set.
    /// </summary>
    UnrecognizedSkill
}

/// <summary>Same Rejected/roll-failed distinction as <see cref="StunAttemptOutcome" /> -- see that type's own remarks.</summary>
public readonly record struct UnstunAttemptOutcome(bool Rejected, UnstunRejectReason RejectReason, bool Success)
{
    public static readonly UnstunAttemptOutcome Failed = new(false, UnstunRejectReason.None, false);
    public static readonly UnstunAttemptOutcome Succeeded = new(false, UnstunRejectReason.None, true);

    public static UnstunAttemptOutcome Reject(UnstunRejectReason reason)
    {
        return new UnstunAttemptOutcome(true, reason, false);
    }
}

/// <summary>
///     Every input <see cref="UnstunResolver.Resolve" /> needs, pre-resolved by the caller (<c>Zone</c>) -- the
///     resolver itself touches no live state.
/// </summary>
/// <param name="TargetIsStunned"><c>PlayerRuntimeState.IsStunned</c> on the target.</param>
/// <param name="UsedSkillId">Wire <c>mAttackActionValue2</c>.</param>
/// <param name="UsedSkillGradePoints">Wire <c>mAttackActionValue3</c> -- already the combined grade.</param>
/// <param name="CurerAnimatingSkillNumber">
///     <c>PlayerRuntimeState.ActionSkillNumber</c> -- what the echo check compares
///     against.
/// </param>
/// <param name="CurerAnimatingGradePoints">
///     <c>PlayerRuntimeState.ActionSkillGradeNum1 + ActionSkillGradeNum2</c> -- what the echo check compares
///     against.
/// </param>
/// <param name="UsedSkill">Resolved from <c>WorldDataCache.SkillsById[UsedSkillId]</c>; null when unknown.</param>
public readonly record struct UnstunAttemptRequest(
    CombatantSnapshot Curer,
    CombatantSnapshot Target,
    bool TargetIsStunned,
    bool TargetPshopOpen,
    bool CurerVerifiesEchoedActionState,
    int UsedSkillId,
    int UsedSkillGradePoints,
    int CurerAnimatingSkillNumber,
    int CurerAnimatingGradePoints,
    SkillDefinition? UsedSkill);

/// <summary>
///     Pure port of <c>ProcessAttack06</c>'s cure-attempt resolution (<c>Server/ts25zone/S07_MyGame02.cpp:3737-3819</c>).
///     Never mutates state itself; the caller (<c>Zone.ApplyUnstunAttack</c>) applies the outcome.
/// </summary>
/// <remarks>
///     Two cited asymmetries versus <see cref="StunResolver" />, both deliberate, not oversights:
///     <list type="bullet">
///         <item>
///             The shared validator runs with its default (self-origin + rate-cap enabled) here, vs. disabled
///             for stun (<c>:3740</c> vs. <c>:3527</c>) -- as with <see cref="StunResolver" />'s own remarks,
///             Fenrir's <c>Zone.ApplyCombatCommand</c> already resolves the "attacker" (here, the curer) as
///             the authenticated session's own character for every combat case, so this asymmetry has no
///             separate observable behavior to reproduce.
///         </item>
///         <item>
///             The curer's own mid-zone-transfer state is never checked (only the target's is,
///             <c>:3758-3773</c>) -- reproduced via <c>World.PlayerRuntimeState.IsMovingZone</c> (surfaced on
///             <see cref="UnstunAttemptRequest.Target" /> only, never <see cref="UnstunAttemptRequest.Curer" />)
///             since the zone-transfer-in-progress-gate behavior contract (2026-07 round). Fenrir's SAME-shard
///             handoff still needs no such check at all (<c>Zone.HandleLeave</c>'s atomic remove-then-post
///             already reproduces a reject there via a plain <c>TryGetPlayer</c> lookup miss), but a
///             CROSS-shard transfer leaves the transferring character live and trackable in the origin
///             shard's own zone for the whole real-world window until its actual disconnect, so a lookup
///             alone no longer reproduces the reject case there -- see
///             <c>World.PlayerRuntimeState.IsMovingZone</c>'s own remarks for the full citation trail.
///         </item>
///     </list>
/// </remarks>
public static class UnstunResolver
{
    /// <summary>A combined grade at/above this guarantees the cure, bypassing the success roll (:3799-3815).</summary>
    public const int GuaranteedCureGradePoints = 20;

    private const int SuccessRollRange = 100;

    /// <summary>
    ///     The stun-resist/cure skill family -- the same triple <see cref="StunResolver.StunResistSkillIds" />
    ///     scans the defender's equipped skills for (<c>S07_MyGame02.cpp:3609-3634</c>); the only ids this
    ///     resolver's own echo check recognizes.
    /// </summary>
    public static readonly ImmutableArray<int> StunResistSkillIds = [5, 24, 43];

    public static UnstunAttemptOutcome Resolve(UnstunAttemptRequest request, IRandomSource rng)
    {
        var curer = request.Curer;
        var target = request.Target;

        if (curer.IsDead)
            return UnstunAttemptOutcome.Reject(UnstunRejectReason.CurerDead);
        if (target.IsDead)
            return UnstunAttemptOutcome.Reject(UnstunRejectReason.TargetDead);
        // Target only -- the curer's own IsMovingZone is never checked, matching legacy (see this type's own
        // remarks). Only observably reachable for a CROSS-shard transfer; a same-shard handoff already
        // removes the target from the zone's player map before this resolver could ever be reached with it.
        if (target.IsMovingZone)
            return UnstunAttemptOutcome.Reject(UnstunRejectReason.TargetMovingZone);
        if (request.TargetPshopOpen)
            return UnstunAttemptOutcome.Reject(UnstunRejectReason.TargetShopOpen);
        if (!request.TargetIsStunned)
            return UnstunAttemptOutcome.Reject(UnstunRejectReason.TargetNotStunned);

        // Alliance not modeled -- same simplification precedent as CombatResolver/PartyRegistry/DuelRegistry.
        // Unconditional: unlike StunResolver's gate, there is no duel exception here at all.
        if (curer.Tribe != target.Tribe)
            return UnstunAttemptOutcome.Reject(UnstunRejectReason.NotAuthorized);

        if (request.CurerVerifiesEchoedActionState)
        {
            if (!StunResistSkillIds.Contains(request.UsedSkillId))
                return UnstunAttemptOutcome.Reject(UnstunRejectReason.UnrecognizedSkill);
            if (request.UsedSkillId != request.CurerAnimatingSkillNumber ||
                request.UsedSkillGradePoints != request.CurerAnimatingGradePoints)
                return UnstunAttemptOutcome.Reject(UnstunRejectReason.AntiCheatEchoMismatch);
        }

        // A combined grade of 20+ is an unconditional success, bypassing the roll (and the skill lookup)
        // entirely -- a "guaranteed cure" tier.
        if (request.UsedSkillGradePoints >= GuaranteedCureGradePoints)
            return UnstunAttemptOutcome.Succeeded;

        if (request.UsedSkill is null)
            return UnstunAttemptOutcome.Failed;

        var doubledValue =
            (int)(SkillCatalog.ReturnSkillValue(request.UsedSkill, request.UsedSkillGradePoints,
                SkillValueKind.StunDefense) * 2);
        var success = doubledValue > 0 && rng.NextInt32(SuccessRollRange) < doubledValue;

        return success ? UnstunAttemptOutcome.Succeeded : UnstunAttemptOutcome.Failed;
    }
}
