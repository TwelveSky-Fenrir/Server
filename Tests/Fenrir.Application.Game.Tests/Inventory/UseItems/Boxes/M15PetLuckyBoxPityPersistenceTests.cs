using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems.Boxes;

public class M15PetLuckyBoxPityPersistenceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const byte RewardSort = 4;

    private static readonly int[] AllRewardIds =
    [
        1012, 1013, 1014, 1015, 1190, 1491, 1492, 506, 507, 508, 509, 578, 579,
        1166, 1118, 1103, 1222, 1145, 1237, 8101, 8102, 8106, 1016
    ];

    private static async Task<UseInventoryItemResponse> RunToCompletionAsync(
        ValueTask<UseInventoryItemResponse> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("LootBoxUseItemHandler task never completed.");
        }

        return await task;
    }

    private static (Zone Zone, PlayerRuntimeState State, DirtyTracker<int> DirtyTracker,
        LootBoxUseItemHandler Handler) SetUp()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var itemsById = new Dictionary<int, ItemDefinition>();
        foreach (var id in AllRewardIds)
            itemsById[id] = new ItemDefinition(WorldDataTestRows.Item(id) with { Sort = RewardSort }, []);
        var worldData = ZoneTestKit.EmptyWorldData(itemsById.ToFrozenDictionary());

        var zone = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker, worldData: worldData);
        var (session, pipe) = ZoneTestKit.CreateSession(CharacterId);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));

        dirtyTracker.DrainAll();

        var characters = new FakeCharacterRepository();
        var eventLog = new FakeEventLogRepository();
        var handler = new LootBoxUseItemHandler(worldData, characters, eventLog,
            NullLogger<LootBoxUseItemHandler>.Instance);

        return (zone, state!, dirtyTracker, handler);
    }

    private static ItemStack Box(int quantity)
    {
        return new ItemStack(M15PetLuckyBox8111RewardTable.BoxId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private static UseItemContext Context(Zone zone, PlayerRuntimeState state, ItemStack item, int value = 0)
    {
        return new UseItemContext(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0, item,
            new ItemDefinition(WorldDataTestRows.Item(item.ItemId), []), value);
    }

    private static void SeedBox(Zone zone, ItemStack box)
    {
        zone.PostInventoryCommand(new InventoryZoneCommand(CharacterId,
            [
                new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0,
                    ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, box))
            ], null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public async Task Open_BelowCeiling_IncrementsCounter_AndMarksProgressionDirtyForPersistence()
    {
        var (zone, state, dirtyTracker, handler) = SetUp();
        state.M15PetLuckyBoxPity = 50;
        var box = Box(1);
        SeedBox(zone, box);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, box), CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(51, after!.M15PetLuckyBoxPity);

        var drained = dirtyTracker.DrainAll();
        Assert.True(drained.TryGetValue(CharacterId, out var flags));
        Assert.Equal(DirtyFlags.Progression, flags & DirtyFlags.Progression);
    }

    [Fact]
    public async Task Open_AtCeiling_ForcesGuaranteedReward_ResetsCounterToZero_AndMarksProgressionDirty()
    {
        var (zone, state, dirtyTracker, handler) = SetUp();
        state.M15PetLuckyBoxPity = M15PetLuckyBox8111RewardTable.PityCeiling - 1;
        var box = Box(1);
        SeedBox(zone, box);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, box), CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(0, after!.M15PetLuckyBoxPity);
        Assert.Contains(after.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values,
            stack => M15PetLuckyBox8111RewardTable.PityRewardItemIds.Contains(stack.ItemId));

        var drained = dirtyTracker.DrainAll();
        Assert.True(drained.TryGetValue(CharacterId, out var flags));
        Assert.Equal(DirtyFlags.Progression, flags & DirtyFlags.Progression);
    }

    [Fact]
    public async Task Open_Bulk_AdvancesCounterByOpenedCount_AndMarksProgressionDirtyOnce()
    {
        var (zone, state, dirtyTracker, handler) = SetUp();
        state.M15PetLuckyBoxPity = 10;
        var box = Box(5);
        SeedBox(zone, box);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, box, 3), CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(13, after!.M15PetLuckyBoxPity);

        var drained = dirtyTracker.DrainAll();
        Assert.True(drained.TryGetValue(CharacterId, out var flags));
        Assert.Equal(DirtyFlags.Progression, flags & DirtyFlags.Progression);
    }

    [Fact]
    public async Task Open_OtherBoxId_DoesNotMarkProgressionDirty_ViaThisMirror()
    {
        var (zone, state, dirtyTracker, handler) = SetUp();
        var box = new ItemStack(601, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        SeedBox(zone, box);

        await handler.HandleAsync(
            new UseItemContext(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0, box,
                new ItemDefinition(WorldDataTestRows.Item(601), []), 0), CancellationToken.None);

        var drained = dirtyTracker.DrainAll();
        if (drained.TryGetValue(CharacterId, out var flags))
            Assert.Equal(DirtyFlags.None, flags & DirtyFlags.Progression);
    }
}
