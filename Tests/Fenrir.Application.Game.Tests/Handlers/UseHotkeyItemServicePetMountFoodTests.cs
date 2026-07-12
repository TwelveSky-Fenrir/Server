using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class UseHotkeyItemServicePetMountFoodTests
{
    private const int PetFoodItemId = 2101;
    private const int MountFoodItemId = 2102;
    private const int PetItemId = 5001;
    private const int AnimalItemId = 5501;
    private const int ExcludedAnimalItemId = 5502;
    private const byte ConsumableSort = HotkeyItemConsumptionResolver.ConsumableItemCategory;
    private const byte PetItemSort = 22;
    private const byte AnimalItemSort = 3;
    private const byte ExcludedAnimalItemSort = 30;
    private const int PetActivityStatSort = 12;
    private const int MountActivityExpStatSort = 71;

    private static byte[] ExpectedFrames(params ReadOnlySpan<AvatarStatUpdateResponse> packets)
    {
        var frameSize = FrameWriter.FrameSizeOf<AvatarStatUpdateResponse>();
        var buffer = new byte[frameSize * packets.Length];
        for (var i = 0; i < packets.Length; i++)
            FrameWriter.WriteFrame(in packets[i], buffer.AsSpan(i * frameSize, frameSize));
        return buffer;
    }

    private static (Zone Zone, PlayerRuntimeState State, FakeDuplexPipe Pipe, FakeCharacterRepository Characters,
        UseHotkeyItemService Service) SetUp(int foodItemId, int potionType1, int potionType2, int quantity = 1)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [foodItemId] = new(
                WorldDataTestRows.Item(foodItemId) with
                {
                    Sort = ConsumableSort, PotionType1 = (short)potionType1, PotionType2 = (short)potionType2
                },
                []),
            [PetItemId] = new(WorldDataTestRows.Item(PetItemId) with { Sort = PetItemSort }, []),
            [AnimalItemId] = new(WorldDataTestRows.Item(AnimalItemId) with { Sort = AnimalItemSort }, []),
            [ExcludedAnimalItemId] = new(
                WorldDataTestRows.Item(ExcludedAnimalItemId) with { Sort = ExcludedAnimalItemSort }, [])
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(itemsById);

        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.SetHotkeySlot(0, 0, new HotkeySlot(HotkeyBindingKind.Item, foodItemId, quantity));

        var characters = new FakeCharacterRepository();
        var service = new UseHotkeyItemService(characters, worldData, NullLogger<UseHotkeyItemService>.Instance);
        return (zone, state, pipe, characters, service);
    }

    private static void EquipPet(PlayerRuntimeState state, int petItemId, int petActivity)
    {
        state.Inventory.ReplaceContainer(ContainerMatrix.Equipment,
            ImmutableDictionary<byte, ItemStack>.Empty.Add(PetSlots.EquipmentSlot,
                new ItemStack(petItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
        state.PetActivity = (byte)petActivity;
    }

    private static void MountAnimal(PlayerRuntimeState state, int animalItemId, int garageSlot, int activity,
        int experience = 0)
    {
        state.AnimalIndex = MountStateResolver.SlotCount + garageSlot;
        state.AnimalNumber = animalItemId;
        state.MountActivity = state.MountActivity.SetItem(garageSlot, activity);
        state.MountAccumulatedExp = state.MountAccumulatedExp.SetItem(garageSlot, experience);
    }

    [Fact]
    public async Task PetFood_PotionType6_Eligible_GrantsActivity_SendsNotification_AndPersistsSlot()
    {
        var (zone, state, pipe, characters, service) = SetUp(PetFoodItemId, 6, 20, quantity: 3);
        EquipPet(state, PetItemId, petActivity: 50);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.Success, outcome);
        Assert.Equal(70, state.PetActivity);

        var sent = ZoneTestKit.DrainOutbound(pipe);
        var expected =
            ExpectedFrames(new AvatarStatUpdateResponse { Sort = PetActivityStatSort, Value = 70, Value2 = 0 });
        Assert.Equal(expected, sent);

        var upsert = Assert.NotNull(characters.LastUpsertHotkeySlot);
        Assert.Equal(2, upsert.Value1);
        Assert.Equal((int)HotkeyBindingKind.Item, upsert.Value2);
    }

    [Fact]
    public async Task PetFood_PotionType6_NoPetEquipped_RejectedCleanly_NoNotification_SlotUntouched()
    {
        var (zone, state, pipe, characters, service) = SetUp(PetFoodItemId, 6, 20, quantity: 3);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.RejectedClean, outcome);
        Assert.Equal(0, state.PetActivity);
        Assert.Null(characters.LastUpsertHotkeySlot);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(3, state.GetHotkeySlot(0, 0).Value2);
    }

    [Fact]
    public async Task PetFood_PotionType6_AlreadyAtActivityCap_RejectedCleanly_NoConsumption()
    {
        var (zone, state, pipe, characters, service) = SetUp(PetFoodItemId, 6, 20, quantity: 1);
        EquipPet(state, PetItemId, petActivity: 100);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.RejectedClean, outcome);
        Assert.Equal(100, state.PetActivity);
        Assert.Null(characters.LastUpsertHotkeySlot);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task PetFood_PotionType6_WrongEquippedItemSort_RejectedCleanly()
    {
        var (zone, state, pipe, characters, service) = SetUp(PetFoodItemId, 6, 20, quantity: 1);
        EquipPet(state, AnimalItemId, petActivity: 10);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.RejectedClean, outcome);
        Assert.Null(characters.LastUpsertHotkeySlot);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task MountFood_PotionType16_Eligible_GrantsActivity_SendsPackedActivityExpNotification()
    {
        var (zone, state, pipe, characters, service) = SetUp(MountFoodItemId, 16, 15, quantity: 2);
        MountAnimal(state, AnimalItemId, garageSlot: 0, activity: 40, experience: 1000);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.Success, outcome);
        Assert.Equal(55, state.MountActivity[0]);
        Assert.Equal(1000, state.MountAccumulatedExp[0]);

        var sent = ZoneTestKit.DrainOutbound(pipe);
        var expected = ExpectedFrames(new AvatarStatUpdateResponse
            { Sort = MountActivityExpStatSort, Value = 55_001_000, Value2 = 0 });
        Assert.Equal(expected, sent);

        var upsert = Assert.NotNull(characters.LastUpsertHotkeySlot);
        Assert.Equal(1, upsert.Value1);
        Assert.Equal((int)HotkeyBindingKind.Item, upsert.Value2);
    }

    [Fact]
    public async Task MountFood_PotionType16_AnimalIndexNotActive_StillSucceeds_WritesGarageSlotZero()
    {
        var (zone, state, pipe, characters, service) = SetUp(MountFoodItemId, 16, 15, quantity: 1);
        state.AnimalNumber = AnimalItemId;

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.Success, outcome);
        Assert.Equal(15, state.MountActivity[0]);
        Assert.Equal(0, state.MountAccumulatedExp[0]);

        var sent = ZoneTestKit.DrainOutbound(pipe);
        var expected = ExpectedFrames(new AvatarStatUpdateResponse
            { Sort = MountActivityExpStatSort, Value = 15_000_000, Value2 = 0 });
        Assert.Equal(expected, sent);
        Assert.NotNull(characters.LastUpsertHotkeySlot);
    }

    [Fact]
    public async Task MountFood_PotionType16_NoAnimalMounted_RejectedCleanly_NoNotification()
    {
        var (zone, state, pipe, characters, service) = SetUp(MountFoodItemId, 16, 15, quantity: 2);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.RejectedClean, outcome);
        Assert.Null(characters.LastUpsertHotkeySlot);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task MountFood_PotionType16_ExcludedAnimalItemSort_RejectedCleanly()
    {
        var (zone, state, pipe, characters, service) = SetUp(MountFoodItemId, 16, 15, quantity: 2);
        MountAnimal(state, ExcludedAnimalItemId, garageSlot: 0, activity: 40);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.RejectedClean, outcome);
        Assert.Equal(40, state.MountActivity[0]);
        Assert.Null(characters.LastUpsertHotkeySlot);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task MountFood_PotionType16_AmountWouldOvershootCap_ClampsToRemainingHeadroom()
    {
        var (zone, state, pipe, characters, service) = SetUp(MountFoodItemId, 16, 15, quantity: 2);
        MountAnimal(state, AnimalItemId, garageSlot: 0, activity: 90);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.Success, outcome);
        Assert.Equal(100, state.MountActivity[0]);

        var sent = ZoneTestKit.DrainOutbound(pipe);
        var expected = ExpectedFrames(new AvatarStatUpdateResponse
            { Sort = MountActivityExpStatSort, Value = 100_000_000, Value2 = 0 });
        Assert.Equal(expected, sent);
        Assert.NotNull(characters.LastUpsertHotkeySlot);
    }

    [Fact]
    public async Task MountFood_PotionType16_ExactlyFillsToCap_StillSucceeds()
    {
        var (zone, state, pipe, characters, service) = SetUp(MountFoodItemId, 16, 10, quantity: 1);
        MountAnimal(state, AnimalItemId, garageSlot: 0, activity: 90);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.Success, outcome);
        Assert.Equal(100, state.MountActivity[0]);

        var sent = ZoneTestKit.DrainOutbound(pipe);
        var expected = ExpectedFrames(new AvatarStatUpdateResponse
            { Sort = MountActivityExpStatSort, Value = 100_000_000, Value2 = 0 });
        Assert.Equal(expected, sent);
        Assert.NotNull(characters.LastUpsertHotkeySlot);
    }

    [Fact]
    public async Task MountFood_PotionType16_AlreadyAtActivityCap_RejectedCleanly_NoPartialFill()
    {
        var (zone, state, pipe, characters, service) = SetUp(MountFoodItemId, 16, 15, quantity: 2);
        MountAnimal(state, AnimalItemId, garageSlot: 0, activity: 100);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.RejectedClean, outcome);
        Assert.Equal(100, state.MountActivity[0]);
        Assert.Null(characters.LastUpsertHotkeySlot);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }
}
