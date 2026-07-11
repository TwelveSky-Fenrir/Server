using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Gm;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Gm;

public class GmCreateItemServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const short MapId = 1;
    private const int GenericActionDataLength = 130;

    private const int StackableItemId = 5000;
    private const byte StackableSort = 2;

    private const int UniqueItemId = 6000;

    private const int OutOfRangeItemId = 100_000;

    private static async Task RunToCompletionAsync(ValueTask pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("GmCreateItemService.HandleAsync never completed.");
        }

        await task;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeEventLogRepository EventLog) SetUp(short accountGrade)
    {
        var zone = ZoneTestKit.CreateZone(MapId);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId, null, accountGrade);
        session.MarkRegistering();
        session.MarkInWorld();
        session.CurrentZone = zone;

        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, MapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        return (session, pipe, zone, state!, new FakeEventLogRepository());
    }

    private static GmCreateItemService CreateService(FakeEventLogRepository eventLog)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [StackableItemId] = new(WorldDataTestRows.Item(StackableItemId) with { Sort = StackableSort }, []),
            [UniqueItemId] = new(WorldDataTestRows.Item(UniqueItemId), [])
        }.ToFrozenDictionary();

        return new GmCreateItemService(ZoneTestKit.EmptyWorldData(itemsById), eventLog,
            NullLogger<GmCreateItemService>.Instance);
    }

    private static byte[] BasePayloadData(int itemId)
    {
        var data = new byte[GenericActionDataLength];
        new GmCreateItemPayload { ItemId = itemId }.Write(data);
        return data;
    }

    private static byte[] QuantityPayloadData(int itemId, int quantity)
    {
        var data = new byte[GenericActionDataLength];
        new GmCreateItemQuantityPayload { ItemId = itemId, Quantity = quantity }.Write(data);
        return data;
    }

    private static async Task AssertResponseSentAsync(FakeDuplexPipe pipe, GenericActionResponse expected)
    {
        var actual = await PacketAssert.ReadSentBytesAsync(pipe);
        var frame = new byte[FrameWriter.FrameSizeOf<GenericActionResponse>()];
        FrameWriter.WriteFrame(in expected, frame);
        Assert.True(actual.Length >= frame.Length,
            $"Expected at least {frame.Length} bytes on the wire, got {actual.Length}.");
        Assert.Equal(frame, actual[^frame.Length..]);
    }

    [Fact]
    public async Task HandleAsync_CallerNotAdminTier_AbortsWithNoReply_LogsNothing_AndCreatesNoItem()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Elevated);
        var service = CreateService(eventLog);
        var data = BasePayloadData(StackableItemId);

        await RunToCompletionAsync(
            service.HandleAsync(505, data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(DisconnectReason.GmCommandLogout, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Empty(eventLog.LoggedEvents);
        Assert.Equal(0, zone.GroundItemCount);
    }

    [Fact]
    public async Task HandleAsync_ItemIdOutOfRange_SendsRejected_LogsNothing_AndCreatesNoItem()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Admin);
        var service = CreateService(eventLog);
        var data = BasePayloadData(OutOfRangeItemId);

        await RunToCompletionAsync(
            service.HandleAsync(505, data, session, state, zone, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 1, Sort = 505, Data = data, RuneValue = 0 });
        Assert.Empty(eventLog.LoggedEvents);
        Assert.Equal(0, zone.GroundItemCount);
    }

    [Fact]
    public async Task HandleAsync_Sort505_StackableItem_CreatesFullStack_AndLogsOneAuditRow()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Admin);
        var service = CreateService(eventLog);
        var data = BasePayloadData(StackableItemId);

        await RunToCompletionAsync(
            service.HandleAsync(505, data, session, state, zone, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, zone.GroundItemCount);
        await AssertResponseSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = 505, Data = data, RuneValue = 0 });

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)1, logged.EventCode);
        Assert.Equal(EventLogCategory.ItemCreate, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Null(logged.TargetAccountId);
        Assert.Null(logged.TargetCharacterId);
        Assert.Equal(StackableItemId, logged.ItemId);
        Assert.Equal(GroundItemPickupPolicy.MaxStackQuantity, logged.Quantity);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal("GmName=Hero;Value=0;CheckImprove=0;CheckHighImprove=0;CheckSetItem=0", logged.Payload);
    }

    [Fact]
    public async Task HandleAsync_Sort523_StackableItem_InRangeQuantity_CreatesThatQuantity_AndLogsOneAuditRow()
    {
        var (session, _, zone, state, eventLog) = SetUp((short)GmCommandTier.Admin);
        var service = CreateService(eventLog);
        var data = QuantityPayloadData(StackableItemId, 250);

        await RunToCompletionAsync(
            service.HandleAsync(523, data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(1, zone.GroundItemCount);
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(StackableItemId, logged.ItemId);
        Assert.Equal(250, logged.Quantity);
        Assert.Equal((byte)1, logged.Outcome);
    }

    [Fact]
    public async Task HandleAsync_Sort523_StackableItem_OutOfRangeQuantity_CreatesNothing_AndSendsRejected()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Admin);
        var service = CreateService(eventLog);
        var data = QuantityPayloadData(StackableItemId, 1000);

        await RunToCompletionAsync(
            service.HandleAsync(523, data, session, state, zone, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 2, Sort = 523, Data = data, RuneValue = 0 });
        Assert.Equal(0, zone.GroundItemCount);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task HandleAsync_Sort523_StackableItem_NegativeQuantity_CreatesNothing_AndSendsRejected()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Admin);
        var service = CreateService(eventLog);
        var data = QuantityPayloadData(StackableItemId, -5);

        await RunToCompletionAsync(
            service.HandleAsync(523, data, session, state, zone, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 2, Sort = 523, Data = data, RuneValue = 0 });
        Assert.Equal(0, zone.GroundItemCount);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task HandleAsync_Sort523_UniqueItem_NegativeQuantity_CreatesNothing_ButStillSendsAccepted()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Admin);
        var service = CreateService(eventLog);
        var data = QuantityPayloadData(UniqueItemId, -3);

        await RunToCompletionAsync(
            service.HandleAsync(523, data, session, state, zone, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = 523, Data = data, RuneValue = 0 });
        Assert.Equal(0, zone.GroundItemCount);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task HandleAsync_Sort505_UniqueItem_CreatesExactlyOneItem_AndLogsOneAuditRow()
    {
        var (session, _, zone, state, eventLog) = SetUp((short)GmCommandTier.Admin);
        var service = CreateService(eventLog);
        var data = BasePayloadData(UniqueItemId);

        await RunToCompletionAsync(
            service.HandleAsync(505, data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(1, zone.GroundItemCount);
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(UniqueItemId, logged.ItemId);
        Assert.Equal(1, logged.Quantity);
    }

    [Fact]
    public async Task HandleAsync_Sort523_UniqueItem_ExplicitQuantity_CreatesThatManySeparateItems_EachOwnAuditRow()
    {
        var (session, _, zone, state, eventLog) = SetUp((short)GmCommandTier.Admin);
        var service = CreateService(eventLog);
        var data = QuantityPayloadData(UniqueItemId, 3);

        await RunToCompletionAsync(
            service.HandleAsync(523, data, session, state, zone, CancellationToken.None), zone);

        Assert.Equal(3, zone.GroundItemCount);
        Assert.Equal(3, eventLog.LoggedEvents.Count);
        Assert.All(eventLog.LoggedEvents, logged =>
        {
            Assert.Equal(UniqueItemId, logged.ItemId);
            Assert.Equal(1, logged.Quantity);
            Assert.Equal(EventLogCategory.ItemCreate, logged.Category);
        });
    }
}
