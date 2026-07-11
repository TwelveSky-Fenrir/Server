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
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class CraftLegendaryPetServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const int Material1ItemId = 5000;

    private const int WrongSortItemId = 5001;

    private static readonly IReadOnlySet<int> AllPossibleResultItemIds = new HashSet<int>(
        LegendaryPetCraftCatalog.LegendaryPool1
            .Concat(LegendaryPetCraftCatalog.LegendaryPool2)
            .Concat(LegendaryPetCraftCatalog.GuardianPool));

    private static async Task<T> RunToCompletionAsync<T>(ValueTask<T> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("CraftLegendaryPetService task never completed.");
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

    private static CraftLegendaryPetService CreateService(FakeCharacterRepository characters,
        FakeEventLogRepository eventLog)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [Material1ItemId] = new(
                WorldDataTestRows.Item(Material1ItemId) with { Sort = LegendaryPetCraftCatalog.Material1RequiredSort1 },
                []),
            [WrongSortItemId] = new(WorldDataTestRows.Item(WrongSortItemId), [])
        }.ToFrozenDictionary();

        return new CraftLegendaryPetService(characters, ZoneTestKit.EmptyWorldData(itemsById), eventLog,
            NullLogger<CraftLegendaryPetService>.Instance);
    }

    private static ItemStack Item(int itemId, int quantity, int serial)
    {
        return new ItemStack(itemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, serial);
    }

    private static CraftLegendaryPetRequest Packet()
    {
        return new CraftLegendaryPetRequest
        {
            Sort = LegendaryPetCraftCatalog.Sort, Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Page3 = 0,
            Index3 = 2, Page4 = -1, Index4 = -1
        };
    }

    [Fact]
    public async Task Success_LogsAnItemCreateAuditRow_ForTheMintedLegendaryPet()
    {
        var (session, _, zone, state, characters, eventLog) = SetUp();
        state.ContributionPoints = LegendaryPetCraftCatalog.ContributionPointCost;
        SeedInventory(zone,
            (0, Item(Material1ItemId, 0, 901)),
            (1, Item(1878, 1, 902)),
            (2, Item(2150, 1, 903)));
        var service = CreateService(characters, eventLog);

        var result = await RunToCompletionAsync(
            service.ResolveAsync(Packet(), zone, state, CharacterId, AccountId, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(CraftLegendaryPetOutcome.Applied, result.Outcome);
        Assert.Contains(result.ResultItemId, AllPossibleResultItemIds);
        Assert.NotNull(characters.LastReplacedContainer);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)1, logged.EventCode);
        Assert.Equal(EventLogCategory.ItemCreate, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Null(logged.TargetAccountId);
        Assert.Null(logged.TargetCharacterId);
        Assert.Equal(result.ResultItemId, logged.ItemId);
        Assert.Equal(1, logged.Quantity);
        Assert.Equal((byte)1, logged.Outcome);
    }

    [Fact]
    public async Task InsufficientContributionPoints_IsRejected_AndLogsNothing()
    {
        var (session, _, zone, state, characters, eventLog) = SetUp();
        state.ContributionPoints = LegendaryPetCraftCatalog.ContributionPointCost - 1;
        SeedInventory(zone,
            (0, Item(Material1ItemId, 0, 901)),
            (1, Item(1878, 1, 902)),
            (2, Item(2150, 1, 903)));
        var service = CreateService(characters, eventLog);

        var result = await service.ResolveAsync(Packet(), zone, state, CharacterId, AccountId,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(CraftLegendaryPetOutcome.Rejected, result.Outcome);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task WrongMaterial1Sort_IsRejected_AndLogsNothing()
    {
        var (session, _, zone, state, characters, eventLog) = SetUp();
        state.ContributionPoints = LegendaryPetCraftCatalog.ContributionPointCost;
        SeedInventory(zone,
            (0, Item(WrongSortItemId, 0, 901)),
            (1, Item(1878, 1, 902)),
            (2, Item(2150, 1, 903)));
        var service = CreateService(characters, eventLog);

        var result = await service.ResolveAsync(Packet(), zone, state, CharacterId, AccountId,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(CraftLegendaryPetOutcome.Rejected, result.Outcome);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Empty(eventLog.LoggedEvents);
    }
}
