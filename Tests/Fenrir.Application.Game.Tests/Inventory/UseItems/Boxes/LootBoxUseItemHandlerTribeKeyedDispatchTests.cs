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
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems.Boxes;

public class LootBoxUseItemHandlerTribeKeyedDispatchTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const byte RewardSort = 4;


    private static readonly int[] HeavenlyJadeChestFullPoolForTribeZero =
        [2307, 1321, 1324, 1007, 1008, 126, 601, 602, 2249, 506, 508, 509, 578, 579, 1045];


    private static readonly int[] WingLuckyBoxFullPoolForTribeZero =
    [
        213, 216, 2477, 201, 2397, 694, 693, 692, 696, 698, 506, 507, 508, 509, 578, 579, 1166, 1118, 1103, 1222, 1145,
        1237
    ];


    private static readonly int[] LoyKrathongBoxFullPoolForTribeZero =
    [
        1407, 1403, 1404, 90787, 90786, 90788, 826, 619,
        1103, 1237, 1166, 578, 579, 1017, 1018, 1092, 1093, 698, 696, 695
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

    private static (Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Characters) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(CharacterId);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        return (zone, state!, new FakeCharacterRepository());
    }

    private static void SeedInventory(Zone zone, byte page, byte slot, ItemStack item)
    {
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(page, ImmutableDictionary<byte, ItemStack>.Empty.SetItem(slot, item)));
        zone.PostInventoryCommand(new InventoryZoneCommand(CharacterId, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static LootBoxUseItemHandler CreateHandler(FakeCharacterRepository characters,
        FrozenDictionary<int, ItemDefinition>? itemsById = null)
    {
        var worldData = ZoneTestKit.EmptyWorldData(itemsById);
        var eventLog = new FakeEventLogRepository();
        return new LootBoxUseItemHandler(worldData, characters, eventLog, NullLogger<LootBoxUseItemHandler>.Instance);
    }

    private static FrozenDictionary<int, ItemDefinition> ItemsWithRewardIds(params int[] rewardIds)
    {
        var byId = new Dictionary<int, ItemDefinition>();
        foreach (var id in rewardIds)
            byId[id] = new ItemDefinition(WorldDataTestRows.Item(id) with { Sort = RewardSort }, []);
        return byId.ToFrozenDictionary();
    }

    private static ItemStack Box(int itemId, int quantity)
    {
        return new ItemStack(itemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private static UseItemContext Context(Zone zone, PlayerRuntimeState state, byte page, byte index, ItemStack item,
        int value = 0)
    {
        return new UseItemContext(zone, state, CharacterId, AccountId, page, index, item,
            new ItemDefinition(WorldDataTestRows.Item(item.ItemId), []), value);
    }


    [Fact]
    public void HandledItemIds_IncludesTheTwelveCatalogBoxesPlusTheSevenTribeKeyedIds()
    {
        int[] expected =
        [
            601, 602, 635, 2249, 7105, 8112, 8113, 76542, 1240, 8111, 8114, 8115,
            76543, 1378, 1379, 1236, 8005, 8108, 720
        ];
        Assert.Equal(expected.OrderBy(x => x), LootBoxUseItemHandler.HandledItemIds.OrderBy(x => x));
    }


    [Fact]
    public async Task CostumeChest76543_RecognizedPreviousTribe_GrantsTheDeterministicReward_AndConsumesBox()
    {
        var (zone, state, characters) = SetUp();
        state.PreviousTribe = 0;
        var handler = CreateHandler(characters, ItemsWithRewardIds(76524));
        var box = Box(76543, 1);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, box), CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Null(after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
        Assert.Contains(after.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values,
            stack => stack.ItemId == 76524);
    }

    [Theory]
    [InlineData((byte)1, 76525)]
    [InlineData((byte)2, 76526)]
    public async Task CostumeChest76543_OtherRecognizedTribes_MapToTheirOwnDeterministicReward(byte previousTribe,
        int expectedRewardId)
    {
        var (zone, state, characters) = SetUp();
        state.PreviousTribe = previousTribe;
        var handler = CreateHandler(characters, ItemsWithRewardIds(expectedRewardId));
        var box = Box(76543, 1);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, box), CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Contains(after!.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values,
            stack => stack.ItemId == expectedRewardId);
    }

    [Fact]
    public async Task CostumeChest76543_UnrecognizedPreviousTribe_RejectsCleanly_NoConsumption()
    {
        var (zone, state, characters) = SetUp();
        state.PreviousTribe = 99;
        var handler = CreateHandler(characters);
        var box = Box(76543, 1);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        var response = await handler.HandleAsync(
            Context(zone, state, ContainerMatrix.InventoryPage0, 0, box), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Null(characters.LastReplacedContainer);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        var untouched = after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0);
        Assert.NotNull(untouched);
        Assert.Equal(76543, untouched!.Value.ItemId);
        Assert.Equal(1, untouched.Value.Quantity);
    }

    [Fact]
    public async Task CostumeChest76543_Bulk_OpensRequestedCount_EachGrantingTheDeterministicReward()
    {
        var (zone, state, characters) = SetUp();
        state.PreviousTribe = 1;
        var handler = CreateHandler(characters, ItemsWithRewardIds(76525));
        var box = Box(76543, 3);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, box, 2),
                CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        var boxAfter = after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0);
        Assert.NotNull(boxAfter);
        Assert.Equal(1, boxAfter!.Value.Quantity);
        var grantedCount = after.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values
            .Count(stack => stack.ItemId == 76525);
        Assert.Equal(2, grantedCount);
    }


    [Theory]
    [InlineData(1378)]
    [InlineData(1379)]
    public async Task WarlordChest_Level2BelowTwelve_RejectsCleanly_NoConsumption_RegardlessOfTribe(int boxId)
    {
        var (zone, state, characters) = SetUp();
        state.Level2 = 11;
        state.PreviousTribe = 0;
        var handler = CreateHandler(characters);
        var box = Box(boxId, 1);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        var response = await handler.HandleAsync(
            Context(zone, state, ContainerMatrix.InventoryPage0, 0, box), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Null(characters.LastReplacedContainer);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        var untouched = after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0);
        Assert.NotNull(untouched);
        Assert.Equal(boxId, untouched!.Value.ItemId);
    }

    [Fact]
    public async Task WarlordChest_UnrecognizedPreviousTribe_RejectsCleanly_EvenWithLevelGateMet()
    {
        var (zone, state, characters) = SetUp();
        state.Level2 = 12;
        state.PreviousTribe = 99;
        var handler = CreateHandler(characters);
        var box = Box(1379, 1);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        var response = await handler.HandleAsync(
            Context(zone, state, ContainerMatrix.InventoryPage0, 0, box), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task WarlordChest1378_LevelGateMetAndTribeRecognized_GrantsOneOfTheElitePool_AndConsumesBox()
    {
        var (zone, state, characters) = SetUp();
        state.Level2 = 12;
        state.PreviousTribe = 0;
        var pool = WarlordChestRewardTable.ElitePoolsByPreviousTribe[0];
        var handler = CreateHandler(characters, ItemsWithRewardIds(pool.ToArray()));
        var box = Box(1378, 1);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, box), CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Null(after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
        var granted = after.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values.Single();
        Assert.Contains(granted.ItemId, pool);
    }

    [Fact]
    public async Task WarlordChest1379_LevelGateMetAndTribeRecognized_GrantsOneOfTheRarePool_AndConsumesBox()
    {
        var (zone, state, characters) = SetUp();
        state.Level2 = 15;
        state.PreviousTribe = 2;
        var pool = WarlordChestRewardTable.RarePoolsByPreviousTribe[2];
        var handler = CreateHandler(characters, ItemsWithRewardIds(pool.ToArray()));
        var box = Box(1379, 1);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, box), CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        var granted = after!.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values.Single();
        Assert.Contains(granted.ItemId, pool);
    }

    [Fact]
    public async Task WarlordChest1378_NotInBulkWhitelist_IgnoresRequestedBulkValue_OpensExactlyOne()
    {
        var (zone, state, characters) = SetUp();
        state.Level2 = 12;
        state.PreviousTribe = 0;
        var pool = WarlordChestRewardTable.ElitePoolsByPreviousTribe[0];
        var handler = CreateHandler(characters, ItemsWithRewardIds(pool.ToArray()));
        var box = Box(1378, 5);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, box, 3),
                CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        var boxAfter = after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0);
        Assert.NotNull(boxAfter);
        Assert.Equal(4, boxAfter!.Value.Quantity);
        Assert.Single(after.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values,
            stack => pool.Contains(stack.ItemId));
    }

    [Fact]
    public async Task HeavenlyJadeChest1236_RecognizedTribe_GrantsSomeMemberOfTheFullKnownPool_AndConsumesBox()
    {
        var (zone, state, characters) = SetUp();
        state.PreviousTribe = 0;
        var handler = CreateHandler(characters, ItemsWithRewardIds(HeavenlyJadeChestFullPoolForTribeZero));
        var box = Box(1236, 1);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, box), CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Null(after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
        var granted = after.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values.Single();
        Assert.Contains(granted.ItemId, HeavenlyJadeChestFullPoolForTribeZero);
    }

    [Fact]
    public async Task WingLuckyBox8005_RecognizedTribe_GrantsSomeMemberOfTheFullKnownPool_AndConsumesBox()
    {
        var (zone, state, characters) = SetUp();
        state.PreviousTribe = 0;
        var handler = CreateHandler(characters, ItemsWithRewardIds(WingLuckyBoxFullPoolForTribeZero));
        var box = Box(8005, 1);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, box), CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Null(after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
        var granted = after.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values.Single();
        Assert.Contains(granted.ItemId, WingLuckyBoxFullPoolForTribeZero);
    }

    [Fact]
    public async Task LoyKrathongBox8108_SingleOpen_RecognizedTribe_GrantsSomeMemberOfTheFullKnownPool()
    {
        var (zone, state, characters) = SetUp();
        state.PreviousTribe = 0;
        var handler = CreateHandler(characters, ItemsWithRewardIds(LoyKrathongBoxFullPoolForTribeZero));
        var box = Box(8108, 1);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, box), CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Null(after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
        var granted = after.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values.Single();
        Assert.Contains(granted.ItemId, LoyKrathongBoxFullPoolForTribeZero);
    }

    [Fact]
    public async Task
        LoyKrathongBox8108_SECURITY_ClientSuppliedExtremeValue_NeverInfluencesGrantedReward_OnlyClampsBulkCount()
    {
        var (zone, state, characters) = SetUp();
        state.PreviousTribe = 0;
        var handler = CreateHandler(characters, ItemsWithRewardIds(LoyKrathongBoxFullPoolForTribeZero));
        const int stackQuantity = 20;
        var box = Box(8108, stackQuantity);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        const int attackerSuppliedValue = 999_999;

        var response = await RunToCompletionAsync(
            handler.HandleAsync(
                Context(zone, state, ContainerMatrix.InventoryPage0, 0, box, attackerSuppliedValue),
                CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));

        Assert.Null(after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));

        var grantedStacks = after.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values.ToList();
        Assert.Equal(stackQuantity, grantedStacks.Sum(stack => stack.Quantity));

        foreach (var stack in grantedStacks)
        {
            Assert.Contains(stack.ItemId, LoyKrathongBoxFullPoolForTribeZero);
            Assert.NotEqual(attackerSuppliedValue, stack.ItemId);
        }
    }


    [Fact]
    public async Task ChestBox720_RecognizedTribe_RepeatedSingleOpens_EventuallyGrantsATribePoolReward()
    {
        var (zone, state, characters) = SetUp();
        state.PreviousTribe = 0;
        int[] tribeZeroPool = [15157, 15267, 15135, 15179, 15223, 15245, 15289];
        var handler = CreateHandler(characters, ItemsWithRewardIds(tribeZeroPool));

        var sawSuccess = false;
        for (var attempt = 0; attempt < 60 && !sawSuccess; attempt++)
        {
            var box = Box(720, 1);
            SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

            var response = await RunToCompletionAsync(
                handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, box),
                    CancellationToken.None), zone);

            Assert.True(response.Result is 0 or 1);
            if (response.Result != 0)
                continue;

            Assert.True(zone.TryGetPlayer(CharacterId, out var after));
            var granted = after!.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values
                .SingleOrDefault(stack => tribeZeroPool.Contains(stack.ItemId));
            Assert.NotEqual(default, granted);
            sawSuccess = true;
        }

        Assert.True(sawSuccess, "Expected at least one of 60 independent 15%-branch attempts to succeed.");
    }
}
