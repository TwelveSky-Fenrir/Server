using System.Collections.Frozen;
using Fenrir.Domain.Game.Stats.Context;
using Fenrir.Core.Packets.Shared;

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
            ComputeCritical(setNumber, bySlot, cosmetic, mount),
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

        var attackPower = ApplyDrunkAttackPower(baseStats.AttackPower, zone);
        attackPower = ApplyBuffPercent(attackPower, GetBuffPercent(buffs, 0));
        attackPower = ApplyPetDoubleRule(attackPower, pet.AttackPower);
        attackPower += pet.SteppedAttackBonus;
        attackPower += SetBonusTables.CapeIuBonus(bySlot[1], 1, 50f);
        attackPower += RankBuffAttackPowerBonus(zone);
        attackPower += TribeRoleAttackPowerBonus(zone);

        var defensePower = ApplyDrunkDefensePower(baseStats.DefensePower, zone);
        defensePower = ApplyBuffPercent(defensePower, GetBuffPercent(buffs, 1));
        defensePower = ApplyPetDoubleRule(defensePower, pet.DefensePower);
        defensePower += SetBonusTables.CapeIuBonus(bySlot[1], 2, 50f);
        defensePower += RankBuffDefensePowerBonus(zone);
        defensePower += TribeRoleDefensePowerBonus(zone);

        var attackSuccess = ApplyDrunkAttackSuccess(baseStats.AttackSuccess, zone);
        attackSuccess = ApplyBuffPercent(attackSuccess, GetBuffPercent(buffs, 2));
        attackSuccess = ApplyBuffPercent(attackSuccess, GetBuffPercent(buffs, 17));
        attackSuccess += SetBonusTables.GetWrapperAttackSuccessBonus(setNumber);
        attackSuccess += RankBuffAttackSuccessBonus(zone);

        var attackBlock = ApplyBuffPercent(baseStats.AttackBlock, GetBuffPercent(buffs, 3));
        attackBlock = ApplyBuffPercent(attackBlock, GetBuffPercent(buffs, 18));
        attackBlock += SetBonusTables.GetWrapperAttackBlockBonus(setNumber);
        attackBlock += RankBuffAttackBlockBonus(zone);

        var elementAttackPower = ApplyDrunkElementAttack(baseStats.ElementAttackPower, zone);
        elementAttackPower = ApplyBuffPercent(elementAttackPower, GetBuffPercent(buffs, 4));
        elementAttackPower = (int)(elementAttackPower * (titleRank + 100) / 100f);
        elementAttackPower += SetBonusTables.GetWrapperElementAttackPowerBonus(setNumber);
        elementAttackPower += SetBonusTables.CapeIuBonus(bySlot[1], 5, 100f);
        elementAttackPower += RankBuffElementAttackPowerBonus(zone);

        var elementDefensePower = ApplyDrunkElementDefense(baseStats.ElementDefensePower, zone);
        elementDefensePower = ApplyBuffPercent(elementDefensePower, GetBuffPercent(buffs, 5));
        elementDefensePower = (int)(elementDefensePower * (titleRank + 100) / 100f);
        elementDefensePower += SetBonusTables.CapeIuBonus(bySlot[1], 6, 100f);
        elementDefensePower += RankBuffElementDefensePowerBonus(zone);

        var critical = ApplyDrunkCritical(baseStats.Critical, zone);
        critical = ApplyBuffPercent(critical, GetBuffPercent(buffs, 10));
        critical += RebirthCriticalWrapperBonus(attributes.RebirthCount);
        critical += SetBonusTables.GetWrapperCriticalBonus(setNumber);
        critical += SetBonusTables.CapeIuBonus(bySlot[1], 7, 0.5f);
        critical += TribeRoleCriticalBonus(zone);

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
        return item.Sort is 1 or 4;
    }

    private static bool AnyLegendary(EquippedItemSlot?[] bySlot)
    {
        foreach (var slot in bySlot)
            if (slot is { } s && IsLegendary(s.Item))
                return true;
        return false;
    }

    private static int PhoenixFlatBonus(int itemId, int b76005, int b76006, int b76007)
    {
        return itemId switch { 76005 => b76005, 76006 => b76006, 76007 => b76007, _ => 0 };
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
