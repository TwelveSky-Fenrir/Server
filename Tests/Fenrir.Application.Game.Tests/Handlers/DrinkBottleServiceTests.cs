using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.FishingConsumables;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Handlers;

public class DrinkBottleServiceTests
{
    private const byte BottleSort = 26;
    private const int DrunkBottleWithLifePenaltyItemId = 878;
    private const int PlainBottleItemId = 501;

    private static (Zone Zone, PlayerRuntimeState State, WorldDataCache WorldData) SetUp()
    {
        var worldData = WorldDataWithBottleItems();
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, state!, worldData);
    }

    private static WorldDataCache WorldDataWithBottleItems()
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [DrunkBottleWithLifePenaltyItemId] =
                new(WorldDataTestRows.Item(DrunkBottleWithLifePenaltyItemId) with { Sort = BottleSort }, []),
            [PlainBottleItemId] = new(WorldDataTestRows.Item(PlainBottleItemId) with { Sort = BottleSort }, [])
        }.ToFrozenDictionary();

        return ZoneTestKit.EmptyWorldData(itemsById);
    }

    private static void SeedBottleSlot(PlayerRuntimeState state, int slot, int itemId, int count)
    {
        state.BottleSlots = state.BottleSlots.SetItem(slot, (itemId, count));
    }

    private static void EquipPet(Zone zone, int characterId, int petItemId)
    {
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(ContainerMatrix.Equipment,
                ImmutableDictionary<byte, ItemStack>.Empty.SetItem(PetSlots.EquipmentSlot,
                    new ItemStack(petItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1))));
        zone.PostInventoryCommand(new InventoryZoneCommand(characterId, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public void Drink_ChargedSlot_DecrementsCountAndArmsDurationAndActiveIndex()
    {
        var (zone, state, worldData) = SetUp();
        SeedBottleSlot(state, 3, PlainBottleItemId, 5);
        var service = new DrinkBottleService(worldData);

        var result = service.Drink(zone, state, 10, 0, 3);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DrinkBottleOutcome.Success, result.Outcome);
        Assert.Equal(3, result.BottleIndex);
        Assert.Equal(BottleResolver.DrunkDurationTicks, result.DrunkDurationTicks);

        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.Equal((PlainBottleItemId, 4), refreshed!.BottleSlots[3]);
        Assert.Equal(BottleResolver.DrunkDurationTicks, refreshed.DrunkBottleTicksRemaining);
        Assert.Equal(3, refreshed.DrunkBottleIndex);
    }

    [Fact]
    public void Drink_RaisesLifeToTheFreshlyRecomputedMaximum()
    {
        var (zone, state, worldData) = SetUp();
        SeedBottleSlot(state, 0, DrunkBottleWithLifePenaltyItemId, 1);
        state.Life = 1;
        var service = new DrinkBottleService(worldData);

        service.Drink(zone, state, 10, 0, 0);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.NotNull(refreshed!.Stats);
        Assert.Equal(refreshed.Stats!.Value.MaxLife, refreshed.Life);
    }

    [Fact]
    public void Drink_AppliesTheItemsAssociatedStatEffect_ReducingMaxLifeBelowTheUnaffectedBaseline()
    {
        var (zone, state, worldData) = SetUp();
        SeedBottleSlot(state, 0, DrunkBottleWithLifePenaltyItemId, 1);
        state.StatVit = 100;
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo, state.RebirthCount,
            state.Level2);
        var baseline = EquipmentService.RecomputeStats(attributes,
            state.Inventory.GetContainer(ContainerMatrix.Equipment), worldData, state.Buffs, default, state);
        var service = new DrinkBottleService(worldData);

        service.Drink(zone, state, 10, 0, 0);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.True(refreshed!.Stats!.Value.MaxLife < baseline.MaxLife);
    }

    [Fact]
    public void Drink_EmptySlot_IsSilent_AndDoesNotArmTheDuration()
    {
        var (zone, state, worldData) = SetUp();
        var service = new DrinkBottleService(worldData);

        var result = service.Drink(zone, state, 10, 0, 0);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DrinkBottleOutcome.Silent, result.Outcome);
        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.Equal(0, refreshed!.DrunkBottleTicksRemaining);
    }

    [Fact]
    public void Drink_WrongMode_IsRejected()
    {
        var (zone, state, worldData) = SetUp();
        SeedBottleSlot(state, 0, PlainBottleItemId, 5);
        var service = new DrinkBottleService(worldData);

        var result = service.Drink(zone, state, 10, 1, 0);

        Assert.Equal(DrinkBottleOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Drink_PetEquipped_IsRejected_EvenWithAChargedSlot()
    {
        var (zone, state, worldData) = SetUp();
        SeedBottleSlot(state, 0, PlainBottleItemId, 5);
        EquipPet(zone, 10, 900);
        var service = new DrinkBottleService(worldData);

        var result = service.Drink(zone, state, 10, 0, 0);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DrinkBottleOutcome.Rejected, result.Outcome);
        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.Equal((PlainBottleItemId, 5), refreshed!.BottleSlots[0]);
        Assert.Equal(0, refreshed.DrunkBottleTicksRemaining);
    }

    [Fact]
    public void Drink_NoPetEquipped_Succeeds()
    {
        var (zone, state, worldData) = SetUp();
        SeedBottleSlot(state, 0, PlainBottleItemId, 5);
        var service = new DrinkBottleService(worldData);

        var result = service.Drink(zone, state, 10, 0, 0);

        Assert.Equal(DrinkBottleOutcome.Success, result.Outcome);
    }
}
