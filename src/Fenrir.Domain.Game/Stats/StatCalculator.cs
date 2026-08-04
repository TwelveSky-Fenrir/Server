using System.Collections.Frozen;
using Fenrir.Core.Packets.Shared;
using Fenrir.Domain.Game.Items;
using Fenrir.Domain.Game.Stats.Context;

namespace Fenrir.Domain.Game.Stats;

public static partial class StatCalculator
{
    private static readonly LevelRowDto ZeroLevelRow = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public static int ApplyPetDoubleRule(int statValue, int petStatValue)
    {
        return statValue >= petStatValue ? statValue + petStatValue : statValue * 2;
    }

    public static EffectiveStats ComputeBaseStats(
        CharacterBaseAttributes attributes,
        IReadOnlyList<EquippedItemSlot> equipment,
        FrozenDictionary<short, LevelRowDto> levels,
        int legacySetNumber = 0,
        PetStatContribution pet = default,
        CosmeticContext cosmetic = default,
        ZoneContext zone = default,
        ConsumableContext consumable = default,
        MountContext mount = default,
        FrozenDictionary<int, GemSocketRowDto>? gemSocketsByTypeAndValue = null)
    {
        var bySlot = BuildSlotLookup(equipment);
        var setNumber = SetBonusTables.ResolveEffectiveSetNumber(attributes.PreviousTribe, equipment, legacySetNumber);
        var isLegendarySet = AnyLegendary(bySlot);
        var levelRow = GetLevelRow(levels, attributes.CombinedLevel);

        var vitality = ComputeVitality(attributes, bySlot, cosmetic, consumable, mount);
        var strength = ComputeStrength(attributes, bySlot, cosmetic, consumable, mount);
        var ki = ComputeKi(attributes, bySlot, cosmetic, consumable, mount);
        var wisdom = ComputeWisdom(attributes, bySlot, cosmetic, consumable, mount);

        return new EffectiveStats(
            ComputeMaxLife(vitality, levelRow, setNumber, isLegendarySet, attributes.PreviousTribe, bySlot, pet.Life,
                zone, consumable, mount, cosmetic),
            ComputeMaxMana(ki, levelRow, setNumber, bySlot, pet.Mana, zone, consumable, mount),
            ComputeAttackPower(strength, ki, levelRow, setNumber, bySlot, cosmetic, zone, mount, consumable,
                gemSocketsByTypeAndValue),
            ComputeDefensePower(wisdom, levelRow, setNumber, bySlot, cosmetic, zone, mount),
            ComputeAttackSuccess(strength, levelRow, setNumber, bySlot, mount, zone, consumable),
            ComputeAttackBlock(wisdom, vitality, levelRow, setNumber, bySlot, mount, zone, consumable),
            ComputeCritical(setNumber, bySlot, cosmetic, mount, consumable, zone),
            ComputeCriticalDefence(setNumber, attributes.RebirthCount, attributes.Halo, bySlot, cosmetic, mount, zone),
            ComputeLuck(setNumber, bySlot, cosmetic),
            ComputeElementAttackPower(levelRow, setNumber, bySlot, cosmetic, consumable, mount, zone),
            ComputeElementDefensePower(setNumber, bySlot, cosmetic, consumable, mount, zone));
    }

    public static EffectiveStats ComputeEffectiveStats(
        CharacterBaseAttributes attributes,
        IReadOnlyList<EquippedItemSlot> equipment,
        FrozenDictionary<short, LevelRowDto> levels,
        BuffInfo? buffs = null,
        int legacySetNumber = 0,
        PetStatContribution pet = default,
        CosmeticContext cosmetic = default,
        ZoneContext zone = default,
        ConsumableContext consumable = default,
        MountContext mount = default,
        FrozenDictionary<int, GemSocketRowDto>? gemSocketsByTypeAndValue = null)
    {
        var baseStats = ComputeBaseStats(attributes, equipment, levels, legacySetNumber, pet, cosmetic, zone,
            consumable, mount, gemSocketsByTypeAndValue);
        var bySlot = BuildSlotLookup(equipment);
        var setNumber = SetBonusTables.ResolveEffectiveSetNumber(attributes.PreviousTribe, equipment, legacySetNumber);
        var titleRank = attributes.Title % 100;

        var attackPower = ApplyBuffPercent(baseStats.AttackPower, GetBuffPercent(buffs, 0));
        attackPower = ApplyPetDoubleRule(attackPower, pet.AttackPower);
        attackPower += RankBuffAttackPowerBonus(zone);
        attackPower += pet.SteppedAttackBonus;
        attackPower = ApplyDrunkAttackPower(attackPower, zone);
        attackPower += TribeRoleAttackPowerBonus(zone);
        attackPower += SetBonusTables.CapeIuBonus(bySlot[1], 1, 50f);

        var defensePower = ApplyBuffPercent(baseStats.DefensePower, GetBuffPercent(buffs, 1));
        defensePower = ApplyPetDoubleRule(defensePower, pet.DefensePower);
        defensePower += RankBuffDefensePowerBonus(zone);
        defensePower = ApplyDrunkDefensePower(defensePower, zone);
        defensePower += TribeRoleDefensePowerBonus(zone);
        defensePower += SetBonusTables.CapeIuBonus(bySlot[1], 2, 50f);

        var attackSuccess = ApplyBuffPercent(baseStats.AttackSuccess, GetBuffPercent(buffs, 2));
        attackSuccess = ApplyBuffPercent(attackSuccess, GetBuffPercent(buffs, 17));
        attackSuccess += SetBonusTables.GetWrapperAttackSuccessBonus(setNumber);
        attackSuccess += RankBuffAttackSuccessBonus(zone);
        attackSuccess = ApplyDrunkAttackSuccess(attackSuccess, zone);

        var attackBlock = ApplyBuffPercent(baseStats.AttackBlock, GetBuffPercent(buffs, 3));
        attackBlock = ApplyBuffPercent(attackBlock, GetBuffPercent(buffs, 18));
        attackBlock += RankBuffAttackBlockBonus(zone);
        attackBlock += SetBonusTables.GetWrapperAttackBlockBonus(setNumber);

        var elementAttackPower = ApplyBuffPercent(baseStats.ElementAttackPower, GetBuffPercent(buffs, 4));
        elementAttackPower = (int)(elementAttackPower * (float)(titleRank + 100) * 0.01f);
        elementAttackPower += SetBonusTables.GetWrapperElementAttackPowerBonus(setNumber);
        elementAttackPower += RankBuffElementAttackPowerBonus(zone);
        elementAttackPower = ApplyDrunkElementAttack(elementAttackPower, zone);
        elementAttackPower += SetBonusTables.CapeIuBonus(bySlot[1], 5, 100f);

        var elementDefensePower = ApplyBuffPercent(baseStats.ElementDefensePower, GetBuffPercent(buffs, 5));
        elementDefensePower = (int)(elementDefensePower * (float)(titleRank + 100) * 0.01f);
        elementDefensePower += RankBuffElementDefensePowerBonus(zone);
        elementDefensePower = ApplyDrunkElementDefense(elementDefensePower, zone);
        elementDefensePower += SetBonusTables.CapeIuBonus(bySlot[1], 6, 100f);

        var critical = ApplyBuffPercent(baseStats.Critical, GetBuffPercent(buffs, 10));
        critical += RebirthCriticalWrapperBonus(attributes.RebirthCount);
        critical += SetBonusTables.GetWrapperCriticalBonus(setNumber);
        critical += TribeRoleCriticalBonus(zone);
        critical = ApplyDrunkCritical(critical, zone);
        critical += SetBonusTables.CapeIuBonus(bySlot[1], 7, 0.5f);

        var luck = ApplyBuffPercent(baseStats.Luck, GetBuffPercent(buffs, 11));

        return baseStats with
        {
            AttackPower = attackPower,
            DefensePower = defensePower,
            AttackSuccess = attackSuccess,
            AttackBlock = attackBlock,
            Critical = critical,
            Luck = luck,
            ElementAttackPower = elementAttackPower,
            ElementDefensePower = elementDefensePower
        };
    }


    private static bool IsLegendary(ItemRowDto item)
    {
        return ItemSortClassifier.IsLegendaryGrade(item);
    }

    private static int ItemSortClass(ItemRowDto item)
    {
        return ItemSortClassifier.Classify(item);
    }

    private static bool AnyLegendary(EquippedItemSlot?[] bySlot)
    {
        foreach (var slot in bySlot)
            if (slot is { } s && IsLegendary(s.Item))
                return true;
        return false;
    }

    private static EquippedItemSlot?[] BuildSlotLookup(IReadOnlyList<EquippedItemSlot> equipment)
    {
        var bySlot = new EquippedItemSlot?[13];
        foreach (var slot in equipment)
            if (slot.SlotIndex is >= 0 and < 13)
                bySlot[slot.SlotIndex] = slot;
        return bySlot;
    }

    private static LevelRowDto GetLevelRow(FrozenDictionary<short, LevelRowDto> levels, short level)
    {
        if (level is < 1 or > 157) return ZeroLevelRow;
        var clamped = level > 145 ? (short)145 : level;
        return levels.TryGetValue(clamped, out var row) ? row : ZeroLevelRow;
    }

    private static int ApplyBuffPercent(int value, int? buffPercent)
    {
        return buffPercent is not { } pct || pct == 0 ? value : (int)(value * (pct + 100) * 0.01f);
    }

    private static int? GetBuffPercent(BuffInfo? buffs, int slotIndex)
    {
        if (buffs is not { } b) return null;
        var idx = slotIndex * 2;
        return idx >= 0 && idx < b.Buff.Length ? b.Buff[idx] : null;
    }
}
