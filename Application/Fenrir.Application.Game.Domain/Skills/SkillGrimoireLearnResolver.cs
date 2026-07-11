using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Skills;

public static class SkillGrimoireLearnResolver
{
    public enum Outcome
    {
        WrongTribe,
        LevelTooLow,
        AlreadyLearned,
        UnknownSkill,
        InsufficientSkillPoints,

                NoFreeSlot,

        Success
    }

        public static Result Resolve(
        int itemTribeRestriction,
        int itemLevelLimit,
        int itemMartialLevelLimit,
        int? grantedSkillId,
        byte characterPreviousTribe,
        int characterCombinedLevel,
        SkillDefinition? skillDefinition,
        IReadOnlyDictionary<byte, LearnedSkill> learnedSkills,
        int currentSkillPoints)
    {
        if (itemTribeRestriction != EquipItemValidationGate.AnyTribeSentinel &&
            itemTribeRestriction - EquipItemValidationGate.TribeRestrictionOffset != characterPreviousTribe)
            return Result.Fail(Outcome.WrongTribe);

        if (itemLevelLimit + itemMartialLevelLimit > characterCombinedLevel)
            return Result.Fail(Outcome.LevelTooLow);

        if (grantedSkillId is { } requestedSkillId)
            foreach (var learned in learnedSkills.Values)
                if (learned.SkillId == requestedSkillId)
                    return Result.Fail(Outcome.AlreadyLearned);

        if (grantedSkillId is null || skillDefinition is not { } skill || skill.Skill.SkillId != grantedSkillId)
            return Result.Fail(Outcome.UnknownSkill);

        if (currentSkillPoints < skill.Skill.LearnSkillPoint)
            return Result.Fail(Outcome.InsufficientSkillPoints);

        return SkillLearnResolver.TryFindFreeSlotForType(skill.Skill.Type, learnedSkills, out var slot)
            ? new Result(Outcome.Success, slot, skill.Skill.SkillId, skill.Skill.LearnSkillPoint)
            : Result.Fail(Outcome.NoFreeSlot);
    }

    public readonly record struct Result(Outcome Outcome, byte Slot, int SkillId, int Cost)
    {
        public static Result Fail(Outcome outcome)
        {
            return new Result(outcome, 0, 0, 0);
        }
    }
}
