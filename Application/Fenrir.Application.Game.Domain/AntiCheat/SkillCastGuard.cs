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

public static class SkillCastGuard
{
    private const int HotkeyMatchCategory = 1;

    private const int SkillEffectCategory = 2;

    public static SkillCastOffense Evaluate(in SkillCastGuardContext context)
    {
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

        if (context.ClaimedBonusGrade != context.ServerBonusGrade)
            return SkillCastOffense.BonusGradeMismatch;

        if (context.IsRealSkillCast &&
            (context.ClaimedInvestedGrade > context.ServerMaxGrade ||
             context.ClaimedBonusGrade > context.ServerBonusGrade))
            return SkillCastOffense.SkillHack1;

        return SkillCastOffense.None;
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
