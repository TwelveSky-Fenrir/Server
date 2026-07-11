using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.ItemModification;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class CraftPetServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;

    private static async Task<T> RunToCompletionAsync<T>(ValueTask<T> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("CraftPetService task never completed.");
        }

        return await task;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeCharacterRepository Characters, FakeEventLogRepository EventLog) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        return (session, pipe, zone, state!, new FakeCharacterRepository(), new FakeEventLogRepository());
    }

    private static void SeedInventory(Zone zone, params (byte Slot, ItemStack Item)[] slots)
    {
        var content = ImmutableDictionary<byte, ItemStack>.Empty;
        foreach (var (slot, item) in slots)
            content = content.SetItem(slot, item);

        var containers =
            ImmutableArray.Create(new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0, content));
        zone.PostInventoryCommand(new InventoryZoneCommand(CharacterId, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static CraftPetService CreateService(FakeCharacterRepository characters, FakeEventLogRepository eventLog,
        WorldDataCache? worldData = null, ILogger<CraftPetService>? logger = null)
    {
        return new CraftPetService(characters, eventLog, worldData ?? ZoneTestKit.EmptyWorldData(),
            logger ?? NullLogger<CraftPetService>.Instance);
    }

    private static ItemStack Item(int itemId, int serial)
    {
        return new ItemStack(itemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, serial);
    }

    private static CraftPetRequest Recipe3Packet()
    {
        return new CraftPetRequest
        {
            Sort = PetCraftRecipeCatalog.Recipe3Sort, Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Page3 = 0,
            Index3 = 2, Page4 = 0, Index4 = 3
        };
    }

    private static CraftPetRequest Recipe4Packet()
    {
        return new CraftPetRequest
        {
            Sort = PetCraftRecipeCatalog.Recipe4Sort, Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Page3 = -1,
            Index3 = -1, Page4 = -1, Index4 = -1
        };
    }

    [Fact]
    public async Task FourSlotRecipe_Recipe3_Success_LogsAnItemCreateAuditRow_ForTheMintedPet()
    {
        var (session, _, zone, state, characters, eventLog) = SetUp();
        SeedInventory(zone,
            (0, Item(PetCraftRecipeCatalog.Recipe3Material1ItemId, 901)),
            (1, Item(PetCraftRecipeCatalog.Recipe3Material2ItemId, 902)),
            (2, Item(PetCraftRecipeCatalog.Recipe3Material3ItemId, 903)),
            (3, Item(PetCraftRecipeCatalog.Recipe3CatalystItemId, 904)));
        var service = CreateService(characters, eventLog);

        var result = await RunToCompletionAsync(
            service.ResolveFourSlotRecipeAsync(Recipe3Packet(), zone, state, CharacterId, AccountId,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(CraftPetOutcome.Applied, result.Outcome);
        Assert.Equal(PetCraftRecipeCatalog.Recipe3ResultItemId, result.ResultItemId);
        Assert.NotNull(characters.LastReplacedContainer);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)1, logged.EventCode);
        Assert.Equal(EventLogCategory.ItemCreate, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Null(logged.TargetAccountId);
        Assert.Null(logged.TargetCharacterId);
        Assert.Equal(PetCraftRecipeCatalog.Recipe3ResultItemId, logged.ItemId);
        Assert.Equal(1, logged.Quantity);
        Assert.Equal((byte)1, logged.Outcome);
    }

    [Fact]
    public async Task FourSlotRecipe_WrongMaterial_IsRejected_AndLogsNothing()
    {
        var (session, _, zone, state, characters, eventLog) = SetUp();
        SeedInventory(zone,
            (0, Item(999, 901)),
            (1, Item(PetCraftRecipeCatalog.Recipe3Material2ItemId, 902)),
            (2, Item(PetCraftRecipeCatalog.Recipe3Material3ItemId, 903)),
            (3, Item(PetCraftRecipeCatalog.Recipe3CatalystItemId, 904)));
        var service = CreateService(characters, eventLog);

        var result = await service.ResolveFourSlotRecipeAsync(Recipe3Packet(), zone, state, CharacterId, AccountId,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(CraftPetOutcome.Rejected, result.Outcome);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task TwoSlotRecipe_Recipe4_Success_LogsAnItemCreateAuditRow_ForTheMintedPet()
    {
        var (session, _, zone, state, characters, eventLog) = SetUp();
        SeedInventory(zone,
            (0, Item(PetCraftRecipeCatalog.Recipe4MaterialItemId, 901)),
            (1, Item(PetCraftRecipeCatalog.Recipe4MaterialItemId, 902)));
        var service = CreateService(characters, eventLog);

        var result = await RunToCompletionAsync(
            service.ResolveTwoSlotRecipeAsync(Recipe4Packet(), zone, state, CharacterId, AccountId,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(CraftPetOutcome.Applied, result.Outcome);
        Assert.Equal(PetCraftRecipeCatalog.Recipe4ResultItemId, result.ResultItemId);
        Assert.NotNull(characters.LastReplacedContainer);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)2, logged.EventCode);
        Assert.Equal(EventLogCategory.ItemCreate, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Equal(PetCraftRecipeCatalog.Recipe4ResultItemId, logged.ItemId);
        Assert.Equal(1, logged.Quantity);
        Assert.Equal((byte)1, logged.Outcome);
    }

    [Fact]
    public async Task TwoSlotRecipe_WrongMaterial_IsRejected_AndLogsNothing()
    {
        var (session, _, zone, state, characters, eventLog) = SetUp();
        SeedInventory(zone,
            (0, Item(PetCraftRecipeCatalog.Recipe4MaterialItemId, 901)),
            (1, Item(999, 902)));
        var service = CreateService(characters, eventLog);

        var result = await service.ResolveTwoSlotRecipeAsync(Recipe4Packet(), zone, state, CharacterId, AccountId,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(CraftPetOutcome.Rejected, result.Outcome);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Empty(eventLog.LoggedEvents);
    }


    [Fact]
    public async Task FourSlotRecipe_Recipe3_Success_NotableResultItem_LogsNotableCraftNotice()
    {
        var (_, _, zone, state, characters, eventLog) = SetUp();
        SeedInventory(zone,
            (0, Item(PetCraftRecipeCatalog.Recipe3Material1ItemId, 901)),
            (1, Item(PetCraftRecipeCatalog.Recipe3Material2ItemId, 902)),
            (2, Item(PetCraftRecipeCatalog.Recipe3Material3ItemId, 903)),
            (3, Item(PetCraftRecipeCatalog.Recipe3CatalystItemId, 904)));

        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [PetCraftRecipeCatalog.Recipe3ResultItemId] = new(
                WorldDataTestRows.Item(PetCraftRecipeCatalog.Recipe3ResultItemId) with { Type = 4 }, [])
        }.ToFrozenDictionary();

        var logger = new CapturingLogger<CraftPetService>();
        var service = CreateService(characters, eventLog, ZoneTestKit.EmptyWorldData(itemsById), logger);

        var result = await RunToCompletionAsync(
            service.ResolveFourSlotRecipeAsync(Recipe3Packet(), zone, state, CharacterId, AccountId,
                CancellationToken.None), zone);

        Assert.Equal(CraftPetOutcome.Applied, result.Outcome);

        var noticeEntries = logger.Entries.Where(e => e.Message.Contains("Notable-craft notice")).ToList();
        var entry = Assert.Single(noticeEntries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("tribe 1", entry.Message);
        Assert.Contains("Hero", entry.Message);
        Assert.Contains(PetCraftRecipeCatalog.Recipe3ResultItemId.ToString(), entry.Message);
        Assert.Contains("pet-recipe-3", entry.Message);
    }

    [Fact]
    public async Task TwoSlotRecipe_Recipe4_Success_LowTierResultItem_DoesNotLogNotableCraftNotice()
    {
        var (_, _, zone, state, characters, eventLog) = SetUp();
        SeedInventory(zone,
            (0, Item(PetCraftRecipeCatalog.Recipe4MaterialItemId, 901)),
            (1, Item(PetCraftRecipeCatalog.Recipe4MaterialItemId, 902)));

        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [PetCraftRecipeCatalog.Recipe4ResultItemId] =
                new(WorldDataTestRows.Item(PetCraftRecipeCatalog.Recipe4ResultItemId), [])
        }.ToFrozenDictionary();

        var logger = new CapturingLogger<CraftPetService>();
        var service = CreateService(characters, eventLog, ZoneTestKit.EmptyWorldData(itemsById), logger);

        var result = await RunToCompletionAsync(
            service.ResolveTwoSlotRecipeAsync(Recipe4Packet(), zone, state, CharacterId, AccountId,
                CancellationToken.None), zone);

        Assert.Equal(CraftPetOutcome.Applied, result.Outcome);
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("Notable-craft notice"));
    }
}
