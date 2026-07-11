using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems;

public class CpTicketUseItemHandlerTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;

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
                throw new TimeoutException("CpTicketUseItemHandler task never completed.");
        }

        return await task;
    }

    private static (Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Characters,
        FakeEventLogRepository EventLog, CpTicketUseItemHandler Handler) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(CharacterId);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));

        var characters = new FakeCharacterRepository();
        var eventLog = new FakeEventLogRepository();
        var writer = new UseItemInventoryWriter(characters, NullLogger<UseItemInventoryWriter>.Instance);
        var handler = new CpTicketUseItemHandler(writer, eventLog, NullLogger<CpTicketUseItemHandler>.Instance);

        return (zone, state!, characters, eventLog, handler);
    }

    private static ItemStack Ticket(int itemId, int quantity = 1)
    {
        return new ItemStack(itemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 1);
    }

    private static ItemDefinition Definition(int itemId)
    {
        return new ItemDefinition(WorldDataTestRows.Item(itemId), []);
    }

    private static UseItemContext Context(Zone zone, PlayerRuntimeState state, int itemId, ItemStack item,
        int value = 0)
    {
        return new UseItemContext(zone, state, CharacterId, AccountId, 0, 0, item, Definition(itemId), value);
    }

    [Fact]
    public async Task PlainClick_CreditsFiveContributionPoints_ConsumesOneUnit()
    {
        var (zone, state, characters, eventLog, handler) = SetUp();
        state.ContributionPoints = 100;
        var item = Ticket(691, quantity: 10);

        var response = await RunToCompletionAsync(handler.HandleAsync(Context(zone, state, 691, item), CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.Equal(105, response.Value);
        Assert.Equal(1, response.Value2);

        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(105, after!.ContributionPoints);

        Assert.NotNull(characters.LastReplacedContainer);
        Assert.Single(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task BulkRequest_ClampedToOnHandStock_CreditsProportionalAmount()
    {
        var (zone, state, characters, _, handler) = SetUp();
        state.ContributionPoints = 0;
        var item = Ticket(1499, quantity: 3);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, 1499, item, value: 999), CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.Equal(15000, response.Value);
        Assert.Equal(3, response.Value2);

        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(15000, after!.ContributionPoints);
        Assert.Null(after.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
    }

    [Fact]
    public async Task WouldExceedCeiling_FailsCleanly_NoCreditNoConsumption()
    {
        var (zone, state, characters, _, handler) = SetUp();
        state.ContributionPoints = BankedCounterMath.GlobalCeiling;
        var item = Ticket(691, quantity: 1);

        var response = await handler.HandleAsync(Context(zone, state, 691, item), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Equal(BankedCounterMath.GlobalCeiling, state.ContributionPoints);
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task ZeroStackQuantity_FailsCleanly()
    {
        var (zone, state, characters, _, handler) = SetUp();
        state.ContributionPoints = 100;
        var item = Ticket(691, quantity: 0);

        var response = await handler.HandleAsync(Context(zone, state, 691, item), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Equal(100, state.ContributionPoints);
        Assert.Null(characters.LastReplacedContainer);
    }
}
