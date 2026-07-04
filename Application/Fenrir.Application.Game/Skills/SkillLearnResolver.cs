using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Skills;

/// <summary>
///     Pure resolver for CZ_PROCESS_DATA_SEND tSort 202/233 (learn a skill from an NPC's offer) and 203 (skill
///     upgrade). No I/O, no Zone dependency.
/// </summary>
/// <remarks>Every failure branch in the legacy is a Quit(), never a graceful reply -- callers must Abort on any non-success result.</remarks>
public static class SkillLearnResolver
{
    public enum LearnFailure
    {
        None,
        NotOfferedByThisNpc,
        UnknownSkill,
        InsufficientSkillPoints,
        AlreadyLearned,
        NoFreeSlot
    }

    public enum UpgradeFailure
    {
        None,
        InvalidSlot,
        SlotEmpty,
        UnknownSkill,
        InsufficientSkillPoints,
        AlreadyMaxed
    }

    /// <summary>MAX_SKILL_SLOT_NUM (aSkill[40]).</summary>
    public const int MaxSlots = 40;

    /// <summary>NpcSkillOfferRowDto.ArrayKind -- tSort 202, nSkillInfo1.</summary>
    public const byte SkillTree1 = 1;

    /// <summary>NpcSkillOfferRowDto.ArrayKind -- tSort 233, nSkillInfo2.</summary>
    public const byte SkillTree2 = 2;

    public static LearnResult ResolveLearn(
        ImmutableArray<NpcSkillOfferRowDto> npcSkillOffers,
        byte arrayKind,
        int requestedSkillId,
        SkillDefinition? skillDefinition,
        IReadOnlyDictionary<byte, LearnedSkill> learnedSkills,
        int currentSkillPoints)
    {
        var offered = false;
        foreach (var offer in npcSkillOffers)
            if (offer.ArrayKind == arrayKind && offer.SkillId == requestedSkillId)
            {
                offered = true;
                break;
            }

        if (!offered)
            return LearnResult.Fail(LearnFailure.NotOfferedByThisNpc);

        if (skillDefinition is not { } skill)
            return LearnResult.Fail(LearnFailure.UnknownSkill);

        if (currentSkillPoints < skill.Skill.LearnSkillPoint)
            return LearnResult.Fail(LearnFailure.InsufficientSkillPoints);

        foreach (var learned in learnedSkills.Values)
            if (learned.SkillId == requestedSkillId)
                return LearnResult.Fail(LearnFailure.AlreadyLearned);

        return TryFindFreeSlotForType(skill.Skill.Type, learnedSkills, out var slot)
            ? new LearnResult(true, LearnFailure.None, slot, skill.Skill.LearnSkillPoint)
            : LearnResult.Fail(LearnFailure.NoFreeSlot);
    }

    public static UpgradeResult ResolveUpgrade(int slot, IReadOnlyDictionary<byte, LearnedSkill> learnedSkills,
        SkillDefinition? skillDefinition, int currentSkillPoints)
    {
        if (slot is < 0 or >= MaxSlots)
            return UpgradeResult.Fail(UpgradeFailure.InvalidSlot);

        if (!learnedSkills.TryGetValue((byte)slot, out var learned))
            return UpgradeResult.Fail(UpgradeFailure.SlotEmpty);

        if (skillDefinition is not { } skill || skill.Skill.SkillId != learned.SkillId)
            return UpgradeResult.Fail(UpgradeFailure.UnknownSkill);

        if (currentSkillPoints < 1)
            return UpgradeResult.Fail(UpgradeFailure.InsufficientSkillPoints);

        if (learned.Grade >= skill.Skill.MaxUpgradePoint)
            return UpgradeResult.Fail(UpgradeFailure.AlreadyMaxed);

        return new UpgradeResult(true, UpgradeFailure.None, learned.Grade + 1);
    }

    /// <summary>Per-sType slot ranges: 1 -> [0,10), 2 -> [20,30), 3/4 -> try [10,20) then [30,40).</summary>
    private static bool TryFindFreeSlotForType(byte skillType, IReadOnlyDictionary<byte, LearnedSkill> learnedSkills,
        out byte slot)
    {
        switch (skillType)
        {
            case 1:
                return TryFindEmptyInRange(learnedSkills, 0, 10, out slot);
            case 2:
                return TryFindEmptyInRange(learnedSkills, 20, 30, out slot);
            case 3:
            case 4:
                return TryFindEmptyInRange(learnedSkills, 10, 20, out slot) ||
                       TryFindEmptyInRange(learnedSkills, 30, 40, out slot);
            default:
                slot = 0;
                return false;
        }
    }

    private static bool TryFindEmptyInRange(IReadOnlyDictionary<byte, LearnedSkill> learnedSkills, int startInclusive,
        int endExclusive, out byte slot)
    {
        for (var i = startInclusive; i < endExclusive; i++)
            if (!learnedSkills.ContainsKey((byte)i))
            {
                slot = (byte)i;
                return true;
            }

        slot = 0;
        return false;
    }

    public readonly record struct LearnResult(bool Success, LearnFailure Failure, byte Slot, int Cost)
    {
        public static LearnResult Fail(LearnFailure failure)
        {
            return new LearnResult(false, failure, 0, 0);
        }
    }

    public readonly record struct UpgradeResult(bool Success, UpgradeFailure Failure, int NewGrade)
    {
        public static UpgradeResult Fail(UpgradeFailure failure)
        {
            return new UpgradeResult(false, failure, 0);
        }
    }
}
