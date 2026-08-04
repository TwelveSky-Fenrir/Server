using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Skills;

public static class AutoHuntSkillSelectionCleanup
{
    public const int AttackSlotCount = 2;

    public static AutoHunt ClearAll(AutoHunt config)
    {
        return config with
        {
            BuffStore = new int[AutoBuffSkillResolver.SlotCount * 2],
            AttackType = new int[AttackSlotCount * 2]
        };
    }

    public static bool TryRemoveSkill(AutoHunt? config, int skillId, out AutoHunt updated)
    {
        updated = default;
        if (config is not { } current || skillId < 1)
            return false;

        var buffStore = (int[])current.BuffStore.Clone();
        var attackType = (int[])current.AttackType.Clone();
        var changed = ClearMatchingPairs(buffStore, skillId) | ClearMatchingPairs(attackType, skillId);
        if (!changed)
            return false;

        updated = current with { BuffStore = buffStore, AttackType = attackType };
        return true;
    }

    private static bool ClearMatchingPairs(Span<int> selections, int skillId)
    {
        var changed = false;
        for (var index = 0; index + 1 < selections.Length; index += 2)
            if (selections[index] == skillId)
            {
                selections[index] = 0;
                selections[index + 1] = 0;
                changed = true;
            }

        return changed;
    }
}
