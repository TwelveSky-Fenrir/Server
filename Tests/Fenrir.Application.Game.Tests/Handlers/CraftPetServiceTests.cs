using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.ItemModification;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>
///     Drives the real <see cref="CraftPetService" /> (op88, CZ_MAKE_PET_SEND) over a real <see cref="Zone" />;
///     ticks the zone while the service's own <c>PostInventoryCommandAndWaitAsync</c> await is pending, same
///     pattern as <c>CraftItemServiceTests</c>. Covers the game.EventLog (Category=ItemCreate) wiring added on
///     top of the pre-existing pet-fusion craft resolution. Recipe3/Recipe4 are used throughout since neither
///     draws from an <c>IRandomSource</c> (see <see cref="PetCraftResolver.ResolveRecipe3" />'s and
///     <see cref="PetCraftResolver.ResolveRecipe4" />'s own signatures) -- deterministic, no multi-trial loop
///     needed.
/// </summary>
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

    private static CraftPetService CreateService(FakeCharacterRepository characters, FakeEventLogRepository eventLog)
    {
        return new CraftPetService(characters, eventLog, NullLogger<CraftPetService>.Instance);
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
}
