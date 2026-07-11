using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Services.Gm;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Gm;

public class GmClearInventoryServiceTests
{
    private const int CallerId = 10;
    private const int Sort = 701;

    private static ItemStack Stack(int itemId)
    {
        return new ItemStack(itemId, 1, 0, 0, 0, 0, 0, 0, 0, 999, 1);
    }

    [Fact]
    public async Task HandleAsync_CallerNotBasicTier_AbortsWithNoReply_NoDbWrite_NoAuditLog()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CallerId, "NotAGm");
        state.Inventory.ReplaceContainer(ContainerMatrix.InventoryPage0,
            System.Collections.Immutable.ImmutableDictionary<byte, ItemStack>.Empty.Add(0, Stack(1001)));
        var characters = new FakeCharacterRepository();
        var eventLog = new FakeEventLogRepository();
        var service = new GmClearInventoryService(characters, eventLog, NullLogger<GmClearInventoryService>.Instance);

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleAsync(new GmClearInventoryPayload { PageSelector = 0 }, GmBasicTestSupport.RequestData(),
                session, state, zone, CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Empty(eventLog.LoggedEvents);
        Assert.Single(state.Inventory.GetContainer(ContainerMatrix.InventoryPage0));
    }

    [Fact]
    public async Task HandleAsync_PageSelectorZero_ClearsOnlyPage0()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        state.Inventory.ReplaceContainer(ContainerMatrix.InventoryPage0,
            System.Collections.Immutable.ImmutableDictionary<byte, ItemStack>.Empty.Add(0, Stack(1001)));
        state.Inventory.ReplaceContainer(ContainerMatrix.InventoryPage1,
            System.Collections.Immutable.ImmutableDictionary<byte, ItemStack>.Empty.Add(0, Stack(1002)));
        var characters = new FakeCharacterRepository();
        var eventLog = new FakeEventLogRepository();
        var service = new GmClearInventoryService(characters, eventLog, NullLogger<GmClearInventoryService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleAsync(new GmClearInventoryPayload { PageSelector = 0 }, data, session, state, zone,
                CancellationToken.None), zone);

        Assert.Empty(state.Inventory.GetContainer(ContainerMatrix.InventoryPage0));
        Assert.Single(state.Inventory.GetContainer(ContainerMatrix.InventoryPage1));

        Assert.NotNull(characters.LastReplacedContainer);
        Assert.Equal(ContainerMatrix.InventoryPage0, characters.LastReplacedContainer!.Value.Container);
        Assert.Empty(characters.LastReplacedContainer.Value.Items);

        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)13, logged.EventCode);
        Assert.Equal(EventLogCategory.GmAction, logged.Category);
        Assert.Equal(GmBasicTestSupport.AccountId, logged.ActorAccountId);
        Assert.Equal(CallerId, logged.ActorCharacterId);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal("PageSelector=0;ClearedPage0=True;ClearedPage1=False", logged.Payload);
    }

    [Fact]
    public async Task HandleAsync_PageSelectorOne_ClearsOnlyPage1()
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        state.Inventory.ReplaceContainer(ContainerMatrix.InventoryPage0,
            System.Collections.Immutable.ImmutableDictionary<byte, ItemStack>.Empty.Add(0, Stack(1001)));
        state.Inventory.ReplaceContainer(ContainerMatrix.InventoryPage1,
            System.Collections.Immutable.ImmutableDictionary<byte, ItemStack>.Empty.Add(0, Stack(1002)));
        var characters = new FakeCharacterRepository();
        var eventLog = new FakeEventLogRepository();
        var service = new GmClearInventoryService(characters, eventLog, NullLogger<GmClearInventoryService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleAsync(new GmClearInventoryPayload { PageSelector = 1 }, data, session, state, zone,
                CancellationToken.None), zone);

        Assert.Single(state.Inventory.GetContainer(ContainerMatrix.InventoryPage0));
        Assert.Empty(state.Inventory.GetContainer(ContainerMatrix.InventoryPage1));

        Assert.NotNull(characters.LastReplacedContainer);
        Assert.Equal(ContainerMatrix.InventoryPage1, characters.LastReplacedContainer!.Value.Container);
        Assert.Empty(characters.LastReplacedContainer.Value.Items);

        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(999)]
    public async Task HandleAsync_OutOfRangePageSelector_ClearsBothPages_StillAcksSuccess(int pageSelector)
    {
        var (registry, zone) = GmBasicTestSupport.CreateWorld();
        var (session, pipe, state) = GmBasicTestSupport.Enter(zone, CallerId, "TheGm", 1);
        state.Inventory.ReplaceContainer(ContainerMatrix.InventoryPage0,
            System.Collections.Immutable.ImmutableDictionary<byte, ItemStack>.Empty.Add(0, Stack(1001)));
        state.Inventory.ReplaceContainer(ContainerMatrix.InventoryPage1,
            System.Collections.Immutable.ImmutableDictionary<byte, ItemStack>.Empty.Add(0, Stack(1002)));
        var characters = new FakeCharacterRepository();
        var eventLog = new FakeEventLogRepository();
        var service = new GmClearInventoryService(characters, eventLog, NullLogger<GmClearInventoryService>.Instance);
        var data = GmBasicTestSupport.RequestData();

        await GmBasicTestSupport.RunToCompletionAsync(
            service.HandleAsync(new GmClearInventoryPayload { PageSelector = pageSelector }, data, session, state,
                zone, CancellationToken.None), zone);

        Assert.Empty(state.Inventory.GetContainer(ContainerMatrix.InventoryPage0));
        Assert.Empty(state.Inventory.GetContainer(ContainerMatrix.InventoryPage1));

        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = Sort, Data = data, RuneValue = 0 });

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal($"PageSelector={pageSelector};ClearedPage0=True;ClearedPage1=True", logged.Payload);
    }
}
