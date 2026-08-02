using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Skills;

namespace Fenrir.Application.Game.Domain.AntiCheat;

public enum SkillCastOffense : byte
{
    None = 0,

    HotkeyMismatch = 1,

    LearnedSkillMissing = 2,

    BonusGradeMismatch = 3,

    SkillHack1 = 4
}

public enum SkillCastEnforcement : byte
{
    None = 0,

    SilentDrop = 1,

    Disconnect = 2
}

public readonly record struct SkillCastVerdict(SkillCastOffense Offense, SkillCastEnforcement Enforcement)
{
    public static readonly SkillCastVerdict Passed = new(SkillCastOffense.None, SkillCastEnforcement.None);
}

public static class SkillCastGuard
{
    public const int HotkeyBoundCategoryCode = 1;

    public const int SkillEffectCategoryCode = 2;

    public static SkillCastVerdict Evaluate(in SkillCastGuardContext context)
    {
        var preCastVerdict = EvaluatePreCast(context);
        return preCastVerdict.Offense != SkillCastOffense.None ? preCastVerdict : EvaluatePostCast(context);
    }

    public static SkillCastVerdict EvaluatePreCast(in SkillCastGuardContext context)
    {
        if (context.SkillCategoryCode is not (HotkeyBoundCategoryCode or SkillEffectCategoryCode))
            return SkillCastVerdict.Passed;

        if (context.ClaimedSkillNumber == 0)
            return SkillCastVerdict.Passed;

        var isAutoLearnedBranch = context.SkillCategoryCode == SkillEffectCategoryCode && context.IsAutoState;

        if (isAutoLearnedBranch)
        {
            if (!HasMatchingLearnedSkill(context.LearnedSkills, context.ClaimedSkillNumber))
                return new SkillCastVerdict(SkillCastOffense.LearnedSkillMissing, SkillCastEnforcement.Disconnect);
        }
        else if (!HasMatchingActiveHotkey(context.Hotkeys, context.ClaimedSkillNumber, context.ClaimedInvestedGrade))
        {
            return new SkillCastVerdict(SkillCastOffense.HotkeyMismatch, SkillCastEnforcement.Disconnect);
        }

        if (context.ClaimedBonusGrade != context.ServerBonusGrade)
        {
            var enforcement = context.SkillCategoryCode == HotkeyBoundCategoryCode
                ? SkillCastEnforcement.Disconnect
                : SkillCastEnforcement.SilentDrop;
            return new SkillCastVerdict(SkillCastOffense.BonusGradeMismatch, enforcement);
        }

        return SkillCastVerdict.Passed;
    }

    public static SkillCastVerdict EvaluatePostCast(in SkillCastGuardContext context)
    {
        if (context.SkillCategoryCode is not (HotkeyBoundCategoryCode or SkillEffectCategoryCode))
            return SkillCastVerdict.Passed;

        if (context.IsRealSkillCast &&
            (context.ClaimedInvestedGrade > context.ServerMaxGrade ||
             context.ClaimedBonusGrade > context.ServerBonusGrade))
            return new SkillCastVerdict(SkillCastOffense.SkillHack1, SkillCastEnforcement.Disconnect);

        return SkillCastVerdict.Passed;
    }

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

    private static bool HasMatchingLearnedSkill(ImmutableDictionary<byte, LearnedSkill> learnedSkills,
        int claimedSkillNumber)
    {
        foreach (var entry in learnedSkills)
            if (entry.Value.SkillId == claimedSkillNumber)
                return true;

        return false;
    }
}

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
