using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Skills;

public static class SkillGradeAuthority
{
    public const int EquipSlotCount = 13;

    public const int CapeSlotIndex = 1;

    public const int GodOfWarriorCapeItemId = 1404;

    public const int PetSlotIndex = PetSlots.EquipmentSlot;

    private const int GuildBuffFlatBonusType = 3;

    private const int GuildBuffFlatBonusAmount = 1;

    private const int BuffCategorySkillType = 2;

    public static int GetMaxSkillGradeNum(int skillId, IReadOnlyDictionary<byte, LearnedSkill> learnedSkills)
    {
        foreach (var learned in learnedSkills.Values)
            if (learned.SkillId == skillId)
                return learned.Grade;

        return -1;
    }

    public static int GetBonusSkillValue(
        int skillId,
        ReadOnlySpan<ItemDefinition?> equipSlotItems,
        int petPackedValue,
        SkillDefinition? skillDefinition,
        int guildBuffType,
        bool guildBuffActive)
    {
        var total = 0;

        for (var slotIndex = 0; slotIndex < equipSlotItems.Length; slotIndex++)
        {
            var equipped = equipSlotItems[slotIndex];
            if (equipped is null)
                continue;

            foreach (var bonus in equipped.BonusSkills)
                if (bonus.SkillId == skillId)
                    total += bonus.Value;

            total += equipped.Item.CapeInfo3;

            if (slotIndex == CapeSlotIndex && equipped.Item.ItemId == GodOfWarriorCapeItemId)
                total += 2;
        }

        if (PetSlotIndex < equipSlotItems.Length && equipSlotItems[PetSlotIndex] is { } petItem)
        {
            var amuletStatType = StatCalculator.PetBonusSkillStatType(skillId);
            if (amuletStatType != 0)
                total += StatCalculator.PetGradedIuBonus(petItem.Item.ItemId, petItem.Item.Sort, petPackedValue,
                    amuletStatType);

            total += StatCalculator.PetGrowthValueBonusSkillGrade(petPackedValue);
        }

        if (guildBuffActive && guildBuffType == GuildBuffFlatBonusType && IsBuffCategorySkill(skillDefinition))
            total += GuildBuffFlatBonusAmount;

        return total;
    }

    private static bool IsBuffCategorySkill(SkillDefinition? skillDefinition)
    {
        return skillDefinition is not null && skillDefinition.Skill.Type == BuffCategorySkillType;
    }
}
