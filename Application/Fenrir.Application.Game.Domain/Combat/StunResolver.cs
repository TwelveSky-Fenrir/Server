using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Why a stun attempt (<c>mCase</c> 5, <c>ProcessAttack05</c>) was refused before rolling -- each is a silent,
///     packet-less <c>return;</c> in the legacy.
/// </summary>
public enum StunRejectReason
{
    None,
    AttackerDead,
    AttackerProtected,
    DefenderDead,
    DefenderShopOpen,

    /// <summary>
    ///     <c>CheckPossibleAttackTarget</c>'s avatar-target rule (<c>S07_MyGame02.cpp:1692-1716</c>): the
    ///     defender's action-state is exactly the "no action yet" placeholder (0) or the death pose (12) -- a
    ///     second, independent gate beyond <see cref="DefenderDead" />.
    /// </summary>
    DefenderActionStateBlocksTargeting,

    DefenderProtected,

    /// <summary>Neither the open-enemy-tribe gate nor the matched-duel gate held (S07_MyGame02.cpp:3579-3590).</summary>
    NotAuthorized,

    /// <summary>Only ever produced when the attacker's own echo-verification flag is set (:3593-3600).</summary>
    AntiCheatEchoMismatch,

    /// <summary>The used skill's resolved stun-attack value came back below 1 (:3602-3607).</summary>
    NoStunSuccessValue
}

/// <summary>
///     A <see cref="Rejected" /> outcome never reaches the success roll at all. A non-rejected, unsuccessful
///     attempt (<see cref="Success" /> false) is the roll itself losing -- both are silent no-ops to the wire,
///     the distinction only matters to callers/tests inspecting *why*.
/// </summary>
public readonly record struct StunAttemptOutcome(
    bool Rejected,
    StunRejectReason RejectReason,
    bool Success,
    int ResolvedDurationSeconds,
    bool IsTeamStunSkill)
{
    public static StunAttemptOutcome Reject(StunRejectReason reason)
    {
        return new StunAttemptOutcome(true, reason, false, 0, false);
    }

    public static StunAttemptOutcome Fail(bool isTeamStunSkill)
    {
        return new StunAttemptOutcome(false, StunRejectReason.None, false, 0, isTeamStunSkill);
    }
}

/// <summary>
///     Every input <see cref="StunResolver.Resolve" /> needs, pre-resolved by the caller (<c>Zone</c>) from its
///     own player map, <see cref="ZonePvpZoneCatalog" />, and <c>DuelRegistry</c> -- the resolver itself touches
///     no live state.
/// </summary>
/// <param name="DefenderActionSort">
///     The defender's last-accepted avatar action Sort (<c>PlayerRuntimeState.ActionSort</c>) -- see
///     <see cref="StunRejectReason.DefenderActionStateBlocksTargeting" />.
/// </param>
/// <param name="ZoneAllowsEnemyTribeAttack">
///     Resolved via <see cref="ZonePvpZoneCatalog.AllowsEnemyTribeAttack" /> -- reused, not re-derived, per
///     that catalog's own remarks.
/// </param>
/// <param name="AttackerAndDefenderShareActiveDuel">
///     True when both sides have an active, mutually-matched duel with each other specifically (opposite
///     roles, same session) -- resolved via <c>DuelRegistry</c>.
/// </param>
/// <param name="AttackerVerifiesEchoedActionState">
///     <see cref="World.PlayerRuntimeState.VerifyEchoedActionState" /> -- when false, the whole echo check is
///     skipped (matches legacy's own conditional gate).
/// </param>
/// <param name="UsedSkillId">Wire <c>mAttackActionValue2</c>.</param>
/// <param name="UsedSkillGradePoints">Wire <c>mAttackActionValue3</c> -- already the combined grade, not summed here.</param>
/// <param name="AttackerAnimatingSkillNumber">
///     <c>PlayerRuntimeState.ActionSkillNumber</c> -- what the echo check compares
///     against.
/// </param>
/// <param name="AttackerAnimatingGradePoints">
///     <c>PlayerRuntimeState.ActionSkillGradeNum1 + ActionSkillGradeNum2</c> -- what the echo check compares
///     against.
/// </param>
/// <param name="UsedSkill">Resolved from <c>WorldDataCache.SkillsById[UsedSkillId]</c>; null when unknown.</param>
/// <param name="DefenderStunResistSkill">
///     First of <see cref="StunResolver.StunResistSkillIds" /> found among the defender's learned skills
///     (slot-order scan, first match wins); null when the defender has none equipped.
/// </param>
/// <param name="DefenderStunResistGradePoints">
///     The matched learned skill's own <c>Grade</c> -- already a single combined value, not summed.
/// </param>
/// <param name="DefenderHasStunImmunityBuff">
///     Buff slot 13 ("Stun Defense") active -- see <see cref="StunResolver" />'s
///     remarks.
/// </param>
public readonly record struct StunAttemptRequest(
    CombatantSnapshot Attacker,
    CombatantSnapshot Defender,
    int DefenderActionSort,
    bool DefenderPshopOpen,
    bool ZoneAllowsEnemyTribeAttack,
    bool AttackerAndDefenderShareActiveDuel,
    bool AttackerVerifiesEchoedActionState,
    int UsedSkillId,
    int UsedSkillGradePoints,
    int AttackerAnimatingSkillNumber,
    int AttackerAnimatingGradePoints,
    SkillDefinition? UsedSkill,
    SkillDefinition? DefenderStunResistSkill,
    int DefenderStunResistGradePoints,
    bool DefenderHasStunImmunityBuff);

/// <summary>
///     Pure port of <c>ProcessAttack05</c>'s stun-attempt resolution (<c>Server/ts25zone/S07_MyGame02.cpp:3491-3735</c>).
///     Never mutates state itself; the caller (<c>Zone.ApplyStunAttack</c>) applies the outcome (sets
///     <c>PlayerRuntimeState.IsStunned</c>/<c>StunDurationSeconds</c>, broadcasts the stun pose, and runs the
///     team-stun sub-mechanic).
/// </summary>
/// <remarks>
///     Not reproduced here, and why:
///     <list type="bullet">
///         <item>
///             The four-way "war-type zone at a late phase" silent-drop gate (<c>:3498-3525</c>) -- the
///             underlying per-zone war-phase state it reads is not modeled anywhere in Fenrir yet, the same
///             already-flagged gap as <see cref="World.WorldState.WorldStateService" />'s own "no column in
///             this schema yet" note (see that class's remarks, lines 134-136).
///         </item>
///         <item>
///             The packet self-origin/rate-cap check <c>CheckAttackPacket</c> normally performs, which this
///             call site explicitly disables (<c>:3527</c>, seventh argument <c>FALSE</c> vs. the shared
///             validator's own default at <c>:1718</c>) -- Fenrir's <c>Zone.ApplyCombatCommand</c> already
///             resolves the attacker as the authenticated session's own character for every combat case (2/3,
///             and now 5/6), never trusting a wire-supplied attacker index, so there is no observable
///             "self-origin skipped" behavior to reproduce here; and no per-user max-attack-packets-per-tick
///             counter exists anywhere in Fenrir yet for ANY combat case, so there is likewise nothing
///             stun-specific to skip. Both are pre-existing, common gaps, not something unique to stun.
///         </item>
///         <item>
///             Alliance is not modeled in the tribe half of the authorization gate -- only the plain
///             same-tribe guard is reproduced (strictly less restrictive), the identical simplification
///             <see cref="CombatResolver.ResolveEnemyTribeAttack" /> already documents for the analogous
///             enemy-tribe path.
///         </item>
///     </list>
/// </remarks>
public static class StunResolver
{
    /// <summary>The reserved "team stun" skill id (<c>mAttackActionValue2==80</c>).</summary>
    public const int TeamStunSkillId = 80;

    /// <summary>"No action yet" placeholder action-state (CheckPossibleAttackTarget, avatar target rule).</summary>
    private const int NoActionYetSort = 0;

    /// <summary>Death-pose action-state (matches <c>Zone.ApplyDeath</c>'s own broadcast Sort).</summary>
    private const int DeathPoseSort = 12;

    /// <summary>Skill 80 rolls out of 100; every other stun skill rolls out of 500 (:3660-3667).</summary>
    private const int TeamStunRollRange = 100;

    private const int OrdinaryStunRollRange = 500;

    /// <summary>
    ///     The stun-resist skill family scanned among the defender's equipped/learned skills, first match wins
    ///     (<c>S07_MyGame02.cpp:3609-3634</c>) -- also the only ids <c>ProcessAttack06</c>'s own echo check
    ///     recognizes (see <see cref="UnstunResolver.StunResistSkillIds" />, the same triple).
    /// </summary>
    public static readonly ImmutableArray<int> StunResistSkillIds = [5, 24, 43];

    public static StunAttemptOutcome Resolve(StunAttemptRequest request, TimeSpan zoneClock, IRandomSource rng)
    {
        var attacker = request.Attacker;
        var defender = request.Defender;

        if (attacker.IsDead)
            return StunAttemptOutcome.Reject(StunRejectReason.AttackerDead);
        if (attacker.ZoneEntryAtZoneClock is { } attackerEntry &&
            zoneClock - attackerEntry < CombatResolver.ProtectDuration)
            return StunAttemptOutcome.Reject(StunRejectReason.AttackerProtected);

        if (defender.IsDead)
            return StunAttemptOutcome.Reject(StunRejectReason.DefenderDead);
        if (request.DefenderPshopOpen)
            return StunAttemptOutcome.Reject(StunRejectReason.DefenderShopOpen);
        if (request.DefenderActionSort is NoActionYetSort or DeathPoseSort)
            return StunAttemptOutcome.Reject(StunRejectReason.DefenderActionStateBlocksTargeting);
        if (defender.ZoneEntryAtZoneClock is { } defenderEntry &&
            zoneClock - defenderEntry < CombatResolver.ProtectDuration)
            return StunAttemptOutcome.Reject(StunRejectReason.DefenderProtected);

        var authorized = (request.ZoneAllowsEnemyTribeAttack && attacker.Tribe != defender.Tribe) ||
                         request.AttackerAndDefenderShareActiveDuel;
        if (!authorized)
            return StunAttemptOutcome.Reject(StunRejectReason.NotAuthorized);

        if (request.AttackerVerifiesEchoedActionState &&
            (request.UsedSkillId != request.AttackerAnimatingSkillNumber ||
             request.UsedSkillGradePoints != request.AttackerAnimatingGradePoints))
            return StunAttemptOutcome.Reject(StunRejectReason.AntiCheatEchoMismatch);

        if (request.UsedSkill is null)
            return StunAttemptOutcome.Reject(StunRejectReason.NoStunSuccessValue);

        var successValue =
            (int)SkillCatalog.ReturnSkillValue(request.UsedSkill, request.UsedSkillGradePoints,
                SkillValueKind.StunAttack);
        if (successValue < 1)
            return StunAttemptOutcome.Reject(StunRejectReason.NoStunSuccessValue);

        var isTeamStunSkill = request.UsedSkillId == TeamStunSkillId;

        int block;
        if (isTeamStunSkill)
            // Forces a guaranteed roll regardless of any equipped stun-resist skill (:3609-3634) -- takes
            // precedence over the buff-13 override below (legacy checks skill==80 first).
            block = 0;
        else if (request.DefenderHasStunImmunityBuff)
            // Buff slot 13 ("Stun Defense") forces block to a "flat maximum" in the production (LNW33) build
            // (:3645-3653) -- no exact literal constant is cited, so this reproduces the qualitative
            // "practically unstunnable" behavior via a sentinel guaranteed to beat any realistic success
            // value, rather than guessing a specific number.
            block = int.MaxValue;
        else if (request.DefenderStunResistSkill is { } resistSkill)
            block = (int)SkillCatalog.ReturnSkillValue(resistSkill, request.DefenderStunResistGradePoints,
                SkillValueKind.StunDefense);
        else
            block = 0;

        var rollRange = isTeamStunSkill ? TeamStunRollRange : OrdinaryStunRollRange;
        var margin = successValue - block;
        var success = margin > 0 && rng.NextInt32(rollRange) < margin;
        if (!success)
            return StunAttemptOutcome.Fail(isTeamStunSkill);

        var durationSeconds =
            (int)SkillCatalog.ReturnSkillValue(request.UsedSkill, request.UsedSkillGradePoints,
                SkillValueKind.RunTime);
        if (durationSeconds < 1)
            return StunAttemptOutcome.Fail(isTeamStunSkill);

        return new StunAttemptOutcome(false, StunRejectReason.None, true, durationSeconds, isTeamStunSkill);
    }
}
