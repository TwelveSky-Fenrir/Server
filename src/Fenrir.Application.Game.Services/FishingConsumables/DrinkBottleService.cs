using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.GameData;
using Fenrir.Domain.Game.Stats;
using Fenrir.Domain.Game.Stats.Context;

namespace Fenrir.Application.Game.Services.FishingConsumables;

public sealed class DrinkBottleService(WorldDataCache worldData) : IDrinkBottleService
{
    public DrinkBottleResult Drink(Zone zone, PlayerRuntimeState state, int characterId, int sort, int value)
    {
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;

        var resolved = BottleResolver.ResolveDrink(state.BottleSlots, sort, value,
            BottleResolver.MountBlocksDrinking(state.AnimalNumber, state.AnimalAbsorbState));

        switch (resolved.Outcome)
        {
            case BottleResolver.DrinkOutcome.Silent:
                return new DrinkBottleResult(DrinkBottleOutcome.Silent, 0, 0);
            case BottleResolver.DrinkOutcome.Rejected:
                return new DrinkBottleResult(DrinkBottleOutcome.Rejected, 0, 0);
        }

        var drunkItemId = state.BottleSlots[value].ItemId;

        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo, state.RebirthCount,
            state.Level2);

        var zoneOverride = new ZoneContext(state.MapId, state.UseOrnament, RankBuffType: state.RankBuffType,
            TribeRole: state.TribeRole, DrunkStateId: drunkItemId, GuildBuffActive: state.GuildBuffActive,
            GuildId: state.GuildId ?? 0);

        var petContribution = PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
            worldData.ItemsById);

        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution, state, zoneOverride: zoneOverride);

        zone.PostDrinkBottleCommand(new DrinkBottleZoneCommand(characterId, value, resolved.NewCount,
            Math.Min(state.Life, updatedStats.MaxLife), UpdatedStats: updatedStats,
            DrunkTicksRemaining: BottleResolver.DrunkDurationTicks,
            DrunkActiveIndex: value));

        return new DrinkBottleResult(DrinkBottleOutcome.Success, value, BottleResolver.DrunkDurationTicks);
    }
}
