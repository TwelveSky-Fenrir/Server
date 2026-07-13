using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Inventory;

public static class EquipmentService
{
    public static ImmutableArray<EquippedItemSlot> BuildEquippedSlots(
        IReadOnlyDictionary<byte, ItemStack> equipmentContainer,
        FrozenDictionary<int, ItemDefinition> itemsById)
    {
        var builder = ImmutableArray.CreateBuilder<EquippedItemSlot>(equipmentContainer.Count);

        foreach (var (slot, stack) in equipmentContainer)
        {
            if (!itemsById.TryGetValue(stack.ItemId, out var definition))
                continue;

            builder.Add(new EquippedItemSlot(slot, definition.Item, stack.Enchant, stack.Combine, stack.Refine,
                stack.Socket, stack.SocketGem1, stack.SocketGem2, stack.SocketGem3));
        }

        return builder.ToImmutable();
    }

    public static EffectiveStats RecomputeStats(
        CharacterBaseAttributes attributes,
        IReadOnlyDictionary<byte, ItemStack> equipmentContainer,
        WorldDataCache worldData,
        BuffInfo? buffs = null,
        PetStatContribution pet = default,
        PlayerRuntimeState? runtimeState = null,
        ConsumableContext? consumableOverride = null,
        ZoneContext? zoneOverride = null,
        MountContext? mountOverride = null)
    {
        var equipped = BuildEquippedSlots(equipmentContainer, worldData.ItemsById);
        var (cosmetic, zone, consumable, mount) = AssembleStatContexts(runtimeState, worldData);
        if (consumableOverride is { } overrideConsumable)
            consumable = overrideConsumable;
        if (zoneOverride is { } overrideZone)
            zone = overrideZone;
        if (mountOverride is { } overrideMount)
            mount = overrideMount;
        var legacySetNumber = StatCalculator.DetectLegacySetNumber(equipped);
        return StatCalculator.ComputeEffectiveStats(attributes, equipped, worldData.LevelsByLevel, buffs,
            legacySetNumber, pet,
            cosmetic, zone, consumable, mount,
            worldData.GemSocketsByTypeAndValue);
    }

    private static (CosmeticContext Cosmetic, ZoneContext Zone, ConsumableContext Consumable, MountContext Mount)
        AssembleStatContexts(PlayerRuntimeState? state, WorldDataCache worldData)
    {
        if (state is null)
            return (default, default, default, default);

        var costumeFound = worldData.ItemsById.TryGetValue(state.CostumeNumber, out var costumeItem);
        var costumeValue = StatCalculator.ComputeCostumeBaseStatBlock(
            state.CostumeNumber, costumeFound,
            costumeItem?.Item.Vitality ?? 0, costumeItem?.Item.Strength ?? 0,
            costumeItem?.Item.Intelligent ?? 0, costumeItem?.Item.Dexterity ?? 0);

        var cosmetic = new CosmeticContext(
            state.RuneSystem,
            state.RuneSystemStat,
            state.CostumeNumber,
            state.CostumeState,
            state.StellarCoreNumber,
            costumeValue,
            StatCalculator.DecodeCostumeEnchantCs(state.CostumeIndex, state.CostumeDate.AsSpan()));

        var zone = new ZoneContext(
            state.MapId,
            state.UseOrnament,
            RankBuffType: state.RankBuffType,
            TribeRole: state.TribeRole,
            DrunkStateId: ResolveDrunkStateId(state),
            GuildBuffActive: state.GuildBuffActive,
            GuildId: state.GuildId ?? 0);

        var consumable = new ConsumableContext(
            state.EatLifePotion,
            state.EatManaPotion,
            state.EatStrPotion,
            state.EatDexPotion,
            state.EatElePotion);

        var mount = new MountContext(
            state.AnimalNumber,
            AbsorbActive: state.AnimalAbsorbState != 0,
            RuntimeAttributes: state.MountRolledAttributes);

        return (cosmetic, zone, consumable, mount);
    }

    private static int ResolveDrunkStateId(PlayerRuntimeState state)
    {
        return state.DrunkBottleTicksRemaining > 0 && state.DrunkBottleIndex >= 0 &&
               state.DrunkBottleIndex < state.BottleSlots.Length
            ? state.BottleSlots[state.DrunkBottleIndex].ItemId
            : 0;
    }
}
