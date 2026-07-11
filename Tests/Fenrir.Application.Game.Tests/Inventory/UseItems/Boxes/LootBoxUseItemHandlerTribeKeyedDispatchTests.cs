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

/// <summary>
///     Covers workstream C10-remaining-boxes-tribe-keyed: the SEPARATE dispatch path
///     <see cref="LootBoxUseItemHandler" /> now takes for 76543 (Limited Costume Chest) and 1378/1379
///     (Sky/Earth Warlord Chest) -- 3 box ids that do not fit any <see cref="BoxRewardKind" /> and are therefore
///     never spliced into <see cref="LootBoxCatalog" /> itself (see <see cref="LootBoxCatalog.TryGetSpec" />,
///     still null for all 3 -- unchanged by this workstream and asserted by
///     <c>LootBoxCatalogTests.TryGetSpec_TribeKeyedBoxesNotYetIntegrated_ReturnNull</c>). Drives the handler
///     directly (not through <see cref="UseItemHandlerRegistry" />) the same way
///     <c>DungeonKeyUseItemHandlerTests</c> does -- the registry's own composition is exercised separately by
///     <see cref="HandledItemIds_IncludesTheEightCatalogBoxesPlusTheThreeTribeKeyedIds" /> below, which locks in
///     that <c>UseItemHandlerRegistry</c>'s constructor loop needs no edit of its own to pick these 3 up.
///     <para>
///         The reward id itself is never scripted via an injectable <see cref="Random" /> (the handler always
///         draws from <see cref="Random.Shared" />), so tests either pin a scenario whose reward id is fully
///         deterministic regardless of the draw (76543's tribe-mapped id), or register every id in the relevant
///         pool so any draw resolves to a known item and assert pool membership instead of an exact id (1378's
///         14-id elite pool).
///     </para>
/// </summary>
public class LootBoxUseItemHandlerTribeKeyedDispatchTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const byte RewardSort = 4; // arbitrary non-stackable, non-pet sort -- exercises placement only.

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

    // ---- Wiring: HandledItemIds -------------------------------------------------------------------

    [Fact]
    public void HandledItemIds_IncludesTheTwelveCatalogBoxesPlusTheSevenTribeKeyedIds()
    {
        // 12 catalog-registered boxes (workstream C10-remaining-box-pools added 1240/8111/8114/8115 to the
        // original 601/602/635/2249/7105/8112/8113/76542) plus 7 tribe/level-keyed ids dispatched entirely
        // outside the catalog (76543/1378/1379 from C10-remaining-boxes-tribe-keyed, 1236/8005/8108/720 added by
        // C10-remaining-box-pools).
        int[] expected =
        [
            601, 602, 635, 2249, 7105, 8112, 8113, 76542, 1240, 8111, 8114, 8115,
            76543, 1378, 1379, 1236, 8005, 8108, 720
        ];
        Assert.Equal(expected.OrderBy(x => x), LootBoxUseItemHandler.HandledItemIds.OrderBy(x => x));
    }

    // ---- 76543 Limited Costume Chest (deterministic tribe->id map) --------------------------------

    [Fact]
    public async Task CostumeChest76543_RecognizedPreviousTribe_GrantsTheDeterministicReward_AndConsumesBox()
    {
        var (zone, state, characters) = SetUp();
        state.PreviousTribe = 0; // ND -> deterministic reward 76524
        var handler = CreateHandler(characters, ItemsWithRewardIds(76524));
        var box = Box(76543, 1);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, box), CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Null(after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0)); // box consumed, slot cleared
        Assert.Contains(after.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values,
            stack => stack.ItemId == 76524);
    }

    [Theory]
    [InlineData((byte)1, 76525)] // RS
    [InlineData((byte)2, 76526)] // GT
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
        state.PreviousTribe = 99; // not 0/1/2 -- no id table entry, RewardIdOverride returns 0
        var handler = CreateHandler(characters); // no reward ids registered -- irrelevant, roll never succeeds
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
        state.PreviousTribe = 1; // RS -> 76525
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
        Assert.Equal(1, boxAfter!.Value.Quantity); // 3 in stock, 2 opened -> 1 remains
        var grantedCount = after.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values
            .Count(stack => stack.ItemId == 76525);
        Assert.Equal(2, grantedCount);
    }

    // ---- 1378/1379 Sky/Earth Warlord Chest (tribe-keyed pool + G12 level gate) ---------------------

    [Theory]
    [InlineData(1378)]
    [InlineData(1379)]
    public async Task WarlordChest_Level2BelowTwelve_RejectsCleanly_NoConsumption_RegardlessOfTribe(int boxId)
    {
        var (zone, state, characters) = SetUp();
        state.Level2 = 11;
        state.PreviousTribe = 0;
        var handler = CreateHandler(characters); // no reward ids needed -- rejected before any roll
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
        state.PreviousTribe = 0; // ND
        var pool = WarlordChestRewardTable.ElitePoolsByPreviousTribe[0];
        var handler = CreateHandler(characters, ItemsWithRewardIds(pool.ToArray()));
        var box = Box(1378, 1);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, box), CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Null(after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0)); // box consumed, slot cleared
        var granted = after.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values.Single();
        Assert.Contains(granted.ItemId, pool);
    }

    [Fact]
    public async Task WarlordChest1379_LevelGateMetAndTribeRecognized_GrantsOneOfTheRarePool_AndConsumesBox()
    {
        var (zone, state, characters) = SetUp();
        state.Level2 = 15; // above the 12 floor -- still eligible
        state.PreviousTribe = 2; // GT
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
        var box = Box(1378, 5); // a stack of 5
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, box, 3),
                CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        var boxAfter = after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0);
        Assert.NotNull(boxAfter);
        // 1378 is absent from LootBoxCatalog.BulkOpenWhitelist, so a requested count of 3 is ignored entirely:
        // the single-open path always consumes exactly 1, regardless of context.Value.
        Assert.Equal(4, boxAfter!.Value.Quantity);
        Assert.Single(after.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values,
            stack => pool.Contains(stack.ItemId));
    }

    // ---- 1236 Heavenly Jade Chest (workstream C10-remaining-box-pools) -----------------------------
    // Six roll branches, only two of which are tribe-keyed -- unlike 76543/1378/1379, most rolls succeed
    // regardless of tribe, so an end-to-end "unrecognized tribe always rejects" test is not practical without
    // an injectable Random (that exact per-branch fail-closed behavior is already exhaustively covered by
    // HeavenlyJadeChest1236RewardTableTests). This is a wiring sanity check only.

    private static readonly int[] HeavenlyJadeChestFullPoolForTribeZero =
        [2307, 1321, 1324, 1007, 1008, 126, 601, 602, 2249, 506, 508, 509, 578, 579, 1045];

    [Fact]
    public async Task HeavenlyJadeChest1236_RecognizedTribe_GrantsSomeMemberOfTheFullKnownPool_AndConsumesBox()
    {
        var (zone, state, characters) = SetUp();
        state.PreviousTribe = 0; // ND
        var handler = CreateHandler(characters, ItemsWithRewardIds(HeavenlyJadeChestFullPoolForTribeZero));
        var box = Box(1236, 1);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, box), CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Null(after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0)); // box consumed, slot cleared
        var granted = after.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values.Single();
        Assert.Contains(granted.ItemId, HeavenlyJadeChestFullPoolForTribeZero);
    }

    // ---- 8005 Wing Lucky Box (workstream C10-remaining-box-pools) ----------------------------------
    // Every tribe-keyed branch here already fails closed identically in the ONE shared legacy implementation
    // (no divergence to harden) -- exhaustively covered by WingLuckyBox8005RewardTableTests. Wiring sanity
    // check only.

    private static readonly int[] WingLuckyBoxFullPoolForTribeZero =
        [213, 216, 2477, 201, 2397, 694, 693, 692, 696, 698, 506, 507, 508, 509, 578, 579, 1166, 1118, 1103, 1222, 1145, 1237];

    [Fact]
    public async Task WingLuckyBox8005_RecognizedTribe_GrantsSomeMemberOfTheFullKnownPool_AndConsumesBox()
    {
        var (zone, state, characters) = SetUp();
        state.PreviousTribe = 0; // ND
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

    // ---- 8108 Loy Krathong Box (workstream C10-remaining-box-pools) -- SECURITY -------------------
    // The legacy single-open code path for this item contains a confirmed arbitrary-item-grant exploit (see
    // LoyKrathongBox8108RewardTable's own SECURITY remarks): ~32% of single-open rolls silently grant whatever
    // item id the CLIENT supplied in the original request. These tests are the end-to-end proof that Fenrir's
    // handler never reproduces that mechanism -- the client's own UseItemContext.Value is never consulted as a
    // reward id, only ever (safely, clamped) as a bulk-open count.

    private static readonly int[] LoyKrathongBoxFullPoolForTribeZero =
    [
        1407, 1403, 1404, 90787, 90786, 90788, 826, 619,
        1103, 1237, 1166, 578, 579, 1017, 1018, 1092, 1093, 698, 696, 695
    ];

    [Fact]
    public async Task LoyKrathongBox8108_SingleOpen_RecognizedTribe_GrantsSomeMemberOfTheFullKnownPool()
    {
        var (zone, state, characters) = SetUp();
        state.PreviousTribe = 0; // ND
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
    public async Task LoyKrathongBox8108_SECURITY_ClientSuppliedExtremeValue_NeverInfluencesGrantedReward_OnlyClampsBulkCount()
    {
        var (zone, state, characters) = SetUp();
        state.PreviousTribe = 0; // ND
        var handler = CreateHandler(characters, ItemsWithRewardIds(LoyKrathongBoxFullPoolForTribeZero));
        const int stackQuantity = 20;
        var box = Box(8108, stackQuantity);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

        // A malicious/attacker-controlled client sends an enormous "value" -- exactly the field the legacy
        // exploit reads as a reward id on 32% of single-open rolls. Here it can ONLY ever mean "how many boxes
        // to open," clamped to the stock on hand (20), never a reward id: LootBoxOpenResolver.OpenBulk clamps
        // to min(boxStack.Quantity, MaxStackQuantity), and LoyKrathongBox8108RewardTable.Roll -- which has no
        // parameter capable of receiving this value at all (see that table's own reflection-based test) -- is
        // the ONLY source of every granted reward id below.
        const int attackerSuppliedValue = 999_999;

        var response = await RunToCompletionAsync(
            handler.HandleAsync(
                Context(zone, state, ContainerMatrix.InventoryPage0, 0, box, attackerSuppliedValue),
                CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));

        // The whole stock was consumed (the count was clamped to 20, not to the attacker-supplied 999999) --
        // the box slot is gone entirely.
        Assert.Null(after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));

        var grantedStacks = after.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values.ToList();
        Assert.Equal(stackQuantity, grantedStacks.Sum(stack => stack.Quantity));

        foreach (var stack in grantedStacks)
        {
            Assert.Contains(stack.ItemId, LoyKrathongBoxFullPoolForTribeZero);
            Assert.NotEqual(attackerSuppliedValue, stack.ItemId);
        }
    }

    // ---- 720 Chest / Reward Box (workstream C10-remaining-box-pools) --------------------------------
    // ChestBox720RewardTable now fully models both branches (see that type's own remarks and tests for the
    // exhaustive per-branch coverage). This fixture only registers world-data item definitions for the
    // tribe-keyed <15% branch's 7 reward ids, so an 85%-branch roll here still resolves to RewardNotFound --
    // NOT because ChestBox720RewardTable.Roll fails (it now always succeeds for that branch), but because this
    // minimal fixture never registered those ids in ItemsById. This is a wiring sanity check confirming the
    // tribe-keyed subset actually grants a real reward end-to-end.

    [Fact]
    public async Task ChestBox720_RecognizedTribe_RepeatedSingleOpens_EventuallyGrantsATribePoolReward()
    {
        // Only 15% of single opens land in the tribe-keyed branch this fixture registered items for; the other
        // 85% resolves to RewardNotFound in THIS fixture (see the comment above) -- box kept, nothing granted,
        // no exception. NOTE: this is deliberately NOT driven through a single bulk request:
        // LootBoxOpenResolver.OpenBulk's own no-progress guard halts the WHOLE batch the moment any one
        // single-open in the loop fails, so a bulk request here would have only a 15% chance of granting
        // anything at all (the very first roll gates every later one). Instead this re-seeds a fresh 1-count
        // box and calls the handler independently many times -- each attempt is a fully independent roll, so
        // the probability every single one of 60 independent attempts misses the tribe-keyed 15% branch is
        // 0.85^60 ≈ 0.00006, an overwhelmingly reliable (not flaky) proof the tribe-keyed subset actually
        // grants a real reward end-to-end, without requiring an injectable Random.
        var (zone, state, characters) = SetUp();
        state.PreviousTribe = 0; // ND
        int[] tribeZeroPool = [15157, 15267, 15135, 15179, 15223, 15245, 15289];
        var handler = CreateHandler(characters, ItemsWithRewardIds(tribeZeroPool));

        var sawSuccess = false;
        for (var attempt = 0; attempt < 60 && !sawSuccess; attempt++)
        {
            var box = Box(720, 1);
            SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, box);

            // RunToCompletionAsync (not a plain await): a successful grant needs the zone ticked to complete
            // the PersistAndMirrorAsync step's pending PostInventoryCommandAndWaitAsync; a clean reject
            // completes synchronously either way, so this is safe for both outcomes.
            var response = await RunToCompletionAsync(
                handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, box),
                    CancellationToken.None), zone);

            Assert.True(response.Result is 0 or 1); // every attempt is either a clean grant or a clean reject
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
