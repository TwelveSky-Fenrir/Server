using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Skills;

namespace Fenrir.Application.Game.Domain.AntiCheat;

/// <summary>
///     Which anti-cheat offense (if any) an avatar-action skill cast tripped, so the caller can emit one audit
///     record naming the reason. <see cref="None" /> = the cast may proceed; every non-<see cref="None" />
///     value is a rejected cast that, in the hardened Fenrir posture, disconnects the session.
/// </summary>
public enum SkillCastOffense : byte
{
    None = 0,

    /// <summary>
    ///     Claimed (skill#, invested grade) matches no active skill-bound hotkey — category 1, or category 2 while not
    ///     auto-hunting.
    /// </summary>
    HotkeyMismatch = 1,

    /// <summary>Claimed skill# is in none of the avatar's learned-skill slots — category 2 while auto-hunting.</summary>
    LearnedSkillMissing = 2,

    /// <summary>Client-claimed bonus grade (<c>aSkillGradeNum2</c>) disagrees with the server's own computed bonus.</summary>
    BonusGradeMismatch = 3,

    /// <summary>"Skill Hack1": claimed invested grade exceeds the server maximum, or claimed bonus exceeds the server bonus.</summary>
    SkillHack1 = 4
}

/// <summary>
///     Server-authoritative tamper guards on an incoming avatar-action skill cast — the hotkey/learned-slot
///     match, the client-bonus-grade consistency check, and the "Skill Hack1" grade-bound check
///     (<c>Server/ts25zone/S04_MyWork02.cpp:1560-1722</c>). Runs AFTER
///     <c>Movement.CharacterMotionWhitelist</c> has validated the motion and produced its
///     <c>SkillCategoryCode</c>. A pure check: no I/O, no mutation.
/// </summary>
/// <remarks>
///     <b>Deliberate hardening decisions (this is a "harden, never reproduce" surface — findings #3/#4 posture):</b>
///     <list type="number">
///         <item>
///             <description>
///                 <b>Uniform disconnect on a bonus-grade mismatch.</b> Legacy is inconsistent — category 1
///                 disconnects (<c>:1583-1587</c>) but category 2 only silently aborts, its disconnect call left
///                 commented out (<c>:1635-1639</c>). Fenrir collapses this to one server-authoritative response:
///                 a client whose claimed bonus grade contradicts server state is treated as tampered input and
///                 disconnected in BOTH categories. Every non-<see cref="SkillCastOffense.None" /> result maps to a
///                 disconnect — there is no "silently abort the cast" outcome here.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>Validate fully before any mutation.</b> This guard performs no mana deduction. Legacy deducts
///                 mana (<c>:1680-1683</c>) before the Skill-Hack1 check (<c>:1684-1722</c>) that can still
///                 disconnect; deducting then tearing down the session is pointless. In Fenrir the mana charge is
///                 owned by <c>Zone.ApplySkillCastManaCharge</c> (which already applies the insufficient-mana silent
///                 abort via <c>Skills.SkillCastResolver</c>) and must run only after this guard returns
///                 <see cref="SkillCastOffense.None" />.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>Record the offense.</b> Legacy leaves the cheat-action gamelog and the <c>mPLAYUSER</c>
///                 block-user broadcast commented out at the Skill-Hack1 site (<c>:1716-1717</c>). The hardened
///                 Fenrir choice — matching the standing "every anti-cheat offense gets an audit record" rule — is
///                 that the call site emits an <c>eventLog</c> entry for the returned offense before disconnecting.
///                 The legacy cross-session block broadcast is intentionally NOT reproduced here: propagating a
///                 forced block to other processes needs its own cited contract (a disconnect is a sufficient,
///                 self-contained sanction). See wiringManifest.
///             </description>
///         </item>
///     </list>
///     The insufficient-mana silent-abort case (<c>:1646-1650</c>) is NOT modeled here — it is already handled
///     downstream by <c>SkillCastResolver</c>/<c>Zone.ApplySkillCastManaCharge</c> and is a cast rejection, not
///     a tamper signal.
/// </remarks>
public static class SkillCastGuard
{
    /// <summary>Legacy skill-sort category 1: the hotkey-match branch (<c>:1560-1587</c>).</summary>
    private const int HotkeyMatchCategory = 1;

    /// <summary>
    ///     Legacy skill-sort category 2: the skill-effect branch (<c>:1589-1650</c>) — hotkey match when not auto,
    ///     learned-slot match when auto.
    /// </summary>
    private const int SkillEffectCategory = 2;

    /// <summary>
    ///     Evaluates the tamper guards in legacy order. Returns <see cref="SkillCastOffense.None" /> when the
    ///     cast may proceed; any other value is an offense the caller records and then disconnects on.
    /// </summary>
    public static SkillCastOffense Evaluate(in SkillCastGuardContext context)
    {
        // Only skill-sort categories 1 and 2 are validated; codes 0 and 3 fall through entirely (they carry no
        // skill-consistency check in legacy). See Movement.CharacterMotionEvaluation.SkillCategoryCode.
        if (context.SkillCategoryCode is not (HotkeyMatchCategory or SkillEffectCategory))
            return SkillCastOffense.None;

        var isAutoLearnedBranch = context.SkillCategoryCode == SkillEffectCategory && context.IsAutoState;

        if (isAutoLearnedBranch)
        {
            if (!HasMatchingLearnedSkill(context.LearnedSkills, context.ClaimedSkillNumber))
                return SkillCastOffense.LearnedSkillMissing;
        }
        else if (!HasMatchingActiveHotkey(context.Hotkeys, context.ClaimedSkillNumber, context.ClaimedInvestedGrade))
        {
            return SkillCastOffense.HotkeyMismatch;
        }

        // Hardened uniform response (legacy cat-1 disconnect / cat-2 silent-abort collapsed to disconnect).
        if (context.ClaimedBonusGrade != context.ServerBonusGrade)
            return SkillCastOffense.BonusGradeMismatch;

        // "Skill Hack1": only for a real skill cast (skill# != 0 and not one of the enumerated buff/party-buff/
        // bottle special cases -- the six Formation skills 76-81, plus the primary-handler-only Bottle pair
        // skill 1/sort 16, per the wave15 D2-skillhack1-special-cases contract). The caller supplies
        // IsRealSkillCast (see Zone.EvaluateSkillCastTamperGuard, which classifies via
        // FormationSkillCatalog.IsExemptFromGradeBoundCheck) rather than this guard re-deriving the set itself,
        // since the exclusion set is a World-layer concern (FormationSkillCatalog) this AntiCheat-layer guard
        // does not depend on directly.
        if (context.IsRealSkillCast &&
            (context.ClaimedInvestedGrade > context.ServerMaxGrade ||
             context.ClaimedBonusGrade > context.ServerBonusGrade))
            return SkillCastOffense.SkillHack1;

        return SkillCastOffense.None;
    }

    // Active, skill-bound hotkey whose stored skill# AND grade both equal the claimed values (across all pages).
    // Allocation-free: struct enumerator over the immutable dictionary, no LINQ.
    private static bool HasMatchingActiveHotkey(ImmutableDictionary<(byte Page, byte Index), HotkeySlot> hotkeys,
        int claimedSkillNumber, int claimedInvestedGrade)
    {
        foreach (var entry in hotkeys)
        {
            var slot = entry.Value;
            if (slot.Kind == HotkeyBindingKind.Skill && slot.Value1 == claimedSkillNumber &&
                slot.Value2 == claimedInvestedGrade)
                return true;
        }

        return false;
    }

    // Presence of the claimed skill# in any learned-skill slot. The D2 contract specifies presence-by-skill#
    // only for the auto-state branch (:1617-1629); it does not state a grade re-check there, so none is
    // invented — see openQuestions.
    private static bool HasMatchingLearnedSkill(ImmutableDictionary<byte, LearnedSkill> learnedSkills,
        int claimedSkillNumber)
    {
        foreach (var entry in learnedSkills)
            if (entry.Value.SkillId == claimedSkillNumber)
                return true;

        return false;
    }
}

/// <summary>
///     Inputs to <see cref="SkillCastGuard.Evaluate" />. The three "claimed" values come off the client action
///     packet; the two "server" values are computed server-side (legacy <c>GetBonusSkillValue</c> /
///     <c>GetMaxSkillGradeNum</c>) and must NOT be trusted from the wire.
/// </summary>
/// <param name="SkillCategoryCode">
///     The <c>SkillCategoryCode</c> produced by <c>CharacterMotionWhitelist</c> for this
///     action (only 1 and 2 are validated).
/// </param>
/// <param name="IsAutoState">Whether the avatar is in auto-hunt state (selects the learned-slot branch for category 2).</param>
/// <param name="ClaimedSkillNumber">Client <c>aSkillNumber</c>.</param>
/// <param name="ClaimedInvestedGrade">Client <c>aSkillGradeNum1</c> — the claimed invested points.</param>
/// <param name="ClaimedBonusGrade">Client <c>aSkillGradeNum2</c> — the claimed equipment/effect bonus.</param>
/// <param name="ServerBonusGrade">Server-computed bonus for this skill (<c>GetBonusSkillValue</c>).</param>
/// <param name="ServerMaxGrade">Server-computed maximum legal invested grade for this skill (<c>GetMaxSkillGradeNum</c>).</param>
/// <param name="IsRealSkillCast">
///     True when the Skill-Hack1 bound check applies: skill# != 0 and not a
///     buff/party-buff/bottle special case (caller-determined — see
///     <c>Fenrir.Application.Game.Domain.World.FormationSkillCatalog.IsExemptFromGradeBoundCheck</c> for the
///     concrete, fully-cited exclusion set the World-layer caller classifies against).
/// </param>
/// <param name="Hotkeys">The avatar's hotkey slots (page/index → slot).</param>
/// <param name="LearnedSkills">The avatar's learned-skill slots.</param>
public readonly record struct SkillCastGuardContext(
    int SkillCategoryCode,
    bool IsAutoState,
    int ClaimedSkillNumber,
    int ClaimedInvestedGrade,
    int ClaimedBonusGrade,
    int ServerBonusGrade,
    int ServerMaxGrade,
    bool IsRealSkillCast,
    ImmutableDictionary<(byte Page, byte Index), HotkeySlot> Hotkeys,
    ImmutableDictionary<byte, LearnedSkill> LearnedSkills);
