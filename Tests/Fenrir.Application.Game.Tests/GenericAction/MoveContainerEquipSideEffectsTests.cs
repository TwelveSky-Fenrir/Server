using System.Collections.Frozen;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.GenericAction;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.GenericAction;

public class MoveContainerEquipSideEffectsTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const short ZoneNumber = 1;
    private const byte WeaponPartTag = 9;
    private const int RentItemId = 76500;
    private const int NonRentItemId = 70010;

    private static WorldDataCache BuildWorldData(params ItemRowDto[] items)
    {
        return ZoneTestKit.EmptyWorldData(items
            .Select(row => new ItemDefinition(row, []))
            .ToFrozenDictionary(d => d.Item.ItemId));
    }

    private static ItemRowDto WeaponItem(int itemId, byte sort)
    {
        return WorldDataTestRows.Item(itemId) with
        {
            Sort = sort,
            EquipInfo1 = EquipItemValidationGate.AnyTribeSentinel,
            EquipInfo2 = WeaponPartTag
        };
    }

    private static (Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Characters) SetUp(
        WorldDataCache worldData, ItemStack sourceStack)
    {
        var zone = ZoneTestKit.CreateZone(ZoneNumber, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(CharacterId);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, ZoneNumber)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));

        state!.PreviousTribe = 0;
        state.Level = 100;
        state.Level2 = 0;
        state.RebirthCount = 0;
        state.ActionSort = 1;

        state.Inventory.ReplaceContainer(ContainerMatrix.InventoryPage0,
            state.Inventory.GetContainer(ContainerMatrix.InventoryPage0).SetItem(5, sourceStack));

        return (zone, state, new FakeCharacterRepository());
    }

    private static GenericActionService CreateService(WorldDataCache worldData, FakeCharacterRepository characters)
    {
        return new GenericActionService(characters, worldData, new QuestCatalog(worldData), new PartyRegistry(),
            new FakeEventLogRepository(), new FakeAccountVaultRepository(), new TradeRegistry(),
            ZoneTestKit.CreateRegistry(), NullLogger<GenericActionService>.Instance);
    }

    private static byte[] InventoryToWeaponSlotMoveBytes(int requestedQuantity)
    {
        var move = new DefaultPData
        {
            Page1 = ContainerMatrix.InventoryPage0, Index1 = 5, Quantity1 = requestedQuantity,
            Page2 = 0, Index2 = EquipmentSlots.WeaponSlot, XPost2 = 0, YPost2 = 0
        };
        var buffer = new byte[DefaultPData.WireSize];
        move.Write(buffer);
        return buffer;
    }

    private static async Task<T> RunToCompletionAsync<T>(ValueTask<T> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("GenericActionService equip task never completed.");
        }

        return await task;
    }

    [Fact]
    public async Task MoveContainerAsync_ClientRequestsExcessiveQuantity_IgnoredAndFullStackMoves()
    {
        var worldData = BuildWorldData(WeaponItem(NonRentItemId, 13));
        var (zone, state, characters) = SetUp(worldData, new ItemStack(NonRentItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var service = CreateService(worldData, characters);

        var result = await RunToCompletionAsync(
            service.MoveContainerAsync(210, InventoryToWeaponSlotMoveBytes(999), zone, state, CharacterId,
                CancellationToken.None),
            zone);

        Assert.Equal(GenericActionStatus.Succeeded, result.Status);
        Assert.Null(state.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 5));
        var equipped = state.Inventory.GetSlot(ContainerMatrix.Equipment, EquipmentSlots.WeaponSlot);
        Assert.NotNull(equipped);
        Assert.Equal(1, equipped!.Value.Quantity);
    }

    [Fact]
    public async Task MoveContainerAsync_RentItem_MigratesExpireDateToEquipmentSlot()
    {
        var worldData = BuildWorldData(WeaponItem(RentItemId, 13));
        var (zone, state, characters) = SetUp(worldData,
            new ItemStack(RentItemId, 1, 0, 0, 0, 0, 0, 0, 0, 555_000, 0));
        var service = CreateService(worldData, characters);

        var result = await RunToCompletionAsync(
            service.MoveContainerAsync(210, InventoryToWeaponSlotMoveBytes(0), zone, state, CharacterId,
                CancellationToken.None),
            zone);

        Assert.Equal(GenericActionStatus.Succeeded, result.Status);
        var equipped = state.Inventory.GetSlot(ContainerMatrix.Equipment, EquipmentSlots.WeaponSlot);
        Assert.NotNull(equipped);
        Assert.Equal(555_000, equipped!.Value.ExpireDate);
    }

    [Fact]
    public async Task MoveContainerAsync_NonRentItem_ZeroesExpireDateAtEquipmentSlot()
    {
        var worldData = BuildWorldData(WeaponItem(NonRentItemId, 13));
        var (zone, state, characters) = SetUp(worldData,
            new ItemStack(NonRentItemId, 1, 0, 0, 0, 0, 0, 0, 0, 555_000, 0));
        var service = CreateService(worldData, characters);

        var result = await RunToCompletionAsync(
            service.MoveContainerAsync(210, InventoryToWeaponSlotMoveBytes(0), zone, state, CharacterId,
                CancellationToken.None),
            zone);

        Assert.Equal(GenericActionStatus.Succeeded, result.Status);
        var equipped = state.Inventory.GetSlot(ContainerMatrix.Equipment, EquipmentSlots.WeaponSlot);
        Assert.NotNull(equipped);
        Assert.Equal(0, equipped!.Value.ExpireDate);
    }

    [Theory]
    [InlineData((byte)13, 2)]
    [InlineData((byte)17, 2)]
    [InlineData((byte)14, 4)]
    [InlineData((byte)15, 6)]
    [InlineData((byte)6, 0)]
    public async Task MoveContainerAsync_RecomputesActionTypeFromWeaponClassAndResetsIdlePose(byte weaponSort,
        int expectedActionType)
    {
        var worldData = BuildWorldData(WeaponItem(NonRentItemId, weaponSort));
        var (zone, state, characters) = SetUp(worldData,
            new ItemStack(NonRentItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        state.ActionType = 99;
        var service = CreateService(worldData, characters);

        var result = await RunToCompletionAsync(
            service.MoveContainerAsync(210, InventoryToWeaponSlotMoveBytes(0), zone, state, CharacterId,
                CancellationToken.None),
            zone);

        Assert.Equal(GenericActionStatus.Succeeded, result.Status);
        Assert.Equal(expectedActionType, state.ActionType);
        Assert.Equal(1, state.ActionSort);
    }
}
