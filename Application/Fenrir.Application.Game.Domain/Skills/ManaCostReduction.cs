using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Skills;

public static class ManaCostReduction
{
    private const int GodOfWarriorCapeBonus = 50;

    public static int GetRatioPercent(ReadOnlySpan<ItemDefinition?> equipSlotItems)
    {
        var ratio = 0;

        for (var slotIndex = 0; slotIndex < equipSlotItems.Length; slotIndex++)
        {
            if (equipSlotItems[slotIndex] is not { } equipped)
                continue;

            ratio += equipped.Item.CapeInfo1;

            if (slotIndex == SkillGradeAuthority.CapeSlotIndex &&
                equipped.Item.ItemId == SkillGradeAuthority.GodOfWarriorCapeItemId)
                ratio += GodOfWarriorCapeBonus;
        }

        return ratio;
    }
}
