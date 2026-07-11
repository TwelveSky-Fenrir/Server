using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Progression;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems;

public class UseItemFamiliesServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const byte SpecialUseSort = 3;
    private const int TitleTicketItemId = 891;
    private const int PalaceRankTicketItemId = 2193;
    private const int MountBoxItemId = 635;

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
                throw new TimeoutException("UseInventoryItemService task never completed.");
        }

        return await task;
    }

    private static (ZoneClientSession Session, Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Characters)
        SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(CharacterId);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        return (session, zone, state!, new FakeCharacterRepository());
    }

    private static void SeedInventory(Zone zone, byte page, ItemStack item)
    {
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(page, ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, item)));
        zone.PostInventoryCommand(new InventoryZoneCommand(CharacterId, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static UseInventoryItemService CreateService(FakeCharacterRepository characters)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [TitleTicketItemId] = new(WorldDataTestRows.Item(TitleTicketItemId) with { Sort = SpecialUseSort }, []),
            [PalaceRankTicketItemId] =
                new(WorldDataTestRows.Item(PalaceRankTicketItemId) with { Sort = SpecialUseSort }, []),
            [MountBoxItemId] = new(WorldDataTestRows.Item(MountBoxItemId) with { Sort = SpecialUseSort }, [])
        }.ToFrozenDictionary();

        var worldData = ZoneTestKit.EmptyWorldData(itemsById);
        var writer = new UseItemInventoryWriter(characters, NullLogger<UseItemInventoryWriter>.Instance);
        var eventLog = new FakeEventLogRepository();
        var registry = new UseItemHandlerRegistry(
            new TitleUpgradeUseItemHandler(worldData, writer, eventLog,
                NullLogger<TitleUpgradeUseItemHandler>.Instance),
            new TitleRemoveScrollUseItemHandler(worldData, writer, eventLog,
                NullLogger<TitleRemoveScrollUseItemHandler>.Instance),
            new PalaceRankUpgradeUseItemHandler(worldData, writer, eventLog,
                NullLogger<PalaceRankUpgradeUseItemHandler>.Instance),
            new EquipSwapUseItemHandler(worldData, writer, NullLogger<EquipSwapUseItemHandler>.Instance),
            new LootBoxUseItemHandler(worldData, characters, eventLog, NullLogger<LootBoxUseItemHandler>.Instance),
            new ForcedNeutralTribeResetUseItemHandler(characters, writer, new FakeGameServerDirectoryRepository(),
                new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>()), new GameServerOptions(),
                NullLogger<ForcedNeutralTribeResetUseItemHandler>.Instance),
            new TribeScrollTransferUseItemHandler(new FakeTribeConversionRepository(),
                new TribeConversionResolver([], [], []), new PartyRegistry(), new FakeGameServerDirectoryRepository(),
                new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>()), worldData,
                new GameServerOptions(), NullLogger<TribeScrollTransferUseItemHandler>.Instance),
            new CpTicketUseItemHandler(writer, eventLog, NullLogger<CpTicketUseItemHandler>.Instance),
            new EliteDungeonTicketUseItemHandler(writer, eventLog,
                NullLogger<EliteDungeonTicketUseItemHandler>.Instance),
            new DungeonKeyUseItemHandler(writer, eventLog, NullLogger<DungeonKeyUseItemHandler>.Instance),
            new IvyHallTicketUseItemHandler(writer, eventLog, NullLogger<IvyHallTicketUseItemHandler>.Instance),
            new LuckyTicketUseItemHandler(worldData, writer, NullLogger<LuckyTicketUseItemHandler>.Instance),
            new ScrollOfSeekersUseItemHandler(writer, eventLog, NullLogger<ScrollOfSeekersUseItemHandler>.Instance),
            new CostumeStellarCoreUseItemHandler(writer, eventLog,
                NullLogger<CostumeStellarCoreUseItemHandler>.Instance));

        var towerUpgrade = new TowerUpgradeService(new TowerWarState(), characters,
            NullLogger<TowerUpgradeService>.Instance);

        return new UseInventoryItemService(characters, new FakeGuildRepository(), new FakeCashRepository(),
            new FakeOfflineShopRepository(), eventLog, new FakeProxyShopExpirationRelayQueue(),
            Options.Create(new GameServerOptions()), worldData, NullLogger<UseInventoryItemService>.Instance,
            towerUpgrade, registry);
    }

    private static ItemStack Ticket(int itemId, int quantity = 1)
    {
        return new ItemStack(itemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 1);
    }


    [Fact]
    public async Task TitleTicket_AtLevel12WithEnoughCp_AdvancesTitle_Deducts10000Cp_AndConsumesTheTicket()
    {
        var (session, zone, state, characters) = SetUp();
        state.Title = 112;
        state.ContributionPoints = 15000;
        SeedInventory(zone, ContainerMatrix.InventoryPage0, Ticket(TitleTicketItemId));
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);

        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(113, after!.Title);
        Assert.Equal(5000, after.ContributionPoints);
        Assert.Null(after.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
    }

    [Fact]
    public async Task TitleTicket_NotExactlyLevel12_FailsCleanly_NoCpDeduction_NoTitleChange()
    {
        var (session, zone, state, characters) = SetUp();
        state.Title = 111;
        state.ContributionPoints = 15000;
        SeedInventory(zone, ContainerMatrix.InventoryPage0, Ticket(TitleTicketItemId));
        var service = CreateService(characters);

        var response = await service.ResolveAsync(zone, state, CharacterId, AccountId,
            ContainerMatrix.InventoryPage0, 0, 0, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, response.Result);
        Assert.Equal(111, state.Title);
        Assert.Equal(15000, state.ContributionPoints);
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task TitleTicket_InsufficientCp_FailsCleanly_LeavesTicketAndCpUntouched()
    {
        var (session, zone, state, characters) = SetUp();
        state.Title = 112;
        state.ContributionPoints = 9999;
        SeedInventory(zone, ContainerMatrix.InventoryPage0, Ticket(TitleTicketItemId));
        var service = CreateService(characters);

        var response = await service.ResolveAsync(zone, state, CharacterId, AccountId,
            ContainerMatrix.InventoryPage0, 0, 0, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, response.Result);
        Assert.Equal(112, state.Title);
        Assert.Equal(9999, state.ContributionPoints);
        Assert.Null(characters.LastReplacedContainer);
    }


    [Fact]
    public async Task PalaceRankTicket_BelowCeiling_RaisesRankByOne_AndConsumesTheTicket()
    {
        var (session, zone, state, characters) = SetUp();
        state.Halo = 50;
        SeedInventory(zone, ContainerMatrix.InventoryPage0, Ticket(PalaceRankTicketItemId));
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);

        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(51, after!.Halo);
        Assert.Null(after.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
    }

    [Fact]
    public async Task PalaceRankTicket_AtCeiling_FailsCleanly_LeavesRankAndTicketUntouched()
    {
        var (session, zone, state, characters) = SetUp();
        state.Halo = 96;
        SeedInventory(zone, ContainerMatrix.InventoryPage0, Ticket(PalaceRankTicketItemId));
        var service = CreateService(characters);

        var response = await service.ResolveAsync(zone, state, CharacterId, AccountId,
            ContainerMatrix.InventoryPage0, 0, 0, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, response.Result);
        Assert.Equal(96, state.Halo);
        Assert.Null(characters.LastReplacedContainer);
    }


    [Fact]
    public async Task MountBox_RollsAReward_NotInThisTestsFakeWorldData_FailsCleanly_WithoutConsumingTheBox()
    {
        var (session, zone, state, characters) = SetUp();
        SeedInventory(zone, ContainerMatrix.InventoryPage0, Ticket(MountBoxItemId));
        var service = CreateService(characters);

        var response = await service.ResolveAsync(zone, state, CharacterId, AccountId,
            ContainerMatrix.InventoryPage0, 0, 0, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, response.Result);
        Assert.Null(characters.LastReplacedContainer);
        Assert.NotNull(state.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
    }


    [Fact]
    public async Task DatedVaultGate_LastPageExpired_FailsCleanly_BeforeAnyFamilyDispatch()
    {
        var (session, zone, state, characters) = SetUp();
        state.InventoryDate = 0;
        state.Title = 112;
        state.ContributionPoints = 15000;
        SeedInventory(zone, ContainerMatrix.InventoryPage1, Ticket(TitleTicketItemId));
        var service = CreateService(characters);

        var response = await service.ResolveAsync(zone, state, CharacterId, AccountId,
            ContainerMatrix.InventoryPage1, 0, 0, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, response.Result);
        Assert.Equal(112, state.Title);
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task DatedVaultGate_LastPageStillValid_LetsDispatchProceed()
    {
        var (session, zone, state, characters) = SetUp();
        state.InventoryDate = GameDate.Today() + 1;
        state.Title = 112;
        state.ContributionPoints = 15000;
        SeedInventory(zone, ContainerMatrix.InventoryPage1, Ticket(TitleTicketItemId));
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage1, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(113, after!.Title);
    }
}
