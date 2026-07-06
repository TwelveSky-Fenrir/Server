using System.Buffers.Binary;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers.Handlers;
using Fenrir.Application.Game.Services.FishingConsumables;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Fishing;

/// <summary>
///     Drives the real <see cref="FishingCatchHandler" /> (opcode 105) over a real <see cref="Zone" />.
///     <see cref="RunToCompletionAsync" /> ticks the zone while the handler's own
///     <c>PostFishingCommandAndWaitAsync</c>/<c>PostInventoryCommandAndWaitAsync</c> awaits are pending --
///     there is no background <c>Zone.RunAsync</c> loop in a unit test to drain those channels otherwise.
/// </summary>
public class FishingCatchHandlerTests
{
    private static readonly int CatchFrame = FrameWriter.FrameSizeOf<FishingCatchResponse>();
    private static readonly int ProgressFrame = FrameWriter.FrameSizeOf<FishingProgressResponse>();
    private static readonly int ActionFrame = FrameWriter.FrameSizeOf<AvatarActionResponse>();

    private static async Task RunToCompletionAsync(ValueTask pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("FishingCatchHandler task never completed.");
        }

        await task;
    }

    private static int ReadInt32(byte[] frame, int frameOffset, int payloadOffset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(frameOffset + 1 + payloadOffset));
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeCharacterRepository Repository) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(FishingLineHandler.FishingZoneNumber);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, FishingLineHandler.FishingZoneNumber)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(10, out var state));
        return (session, pipe, zone, state!, new FakeCharacterRepository());
    }

    [Fact]
    public async Task WrongZone_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var handler = new FishingCatchHandler(new FishingCatchService(new FakeCharacterRepository(),
            NullLogger<FishingCatchService>.Instance));
        await handler.HandleAsync(new FishingCatchRequest(), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task NotCatching_IsSilentAndTouchesNoRepository()
    {
        var (session, pipe, _, _, repo) = SetUp();

        var handler = new FishingCatchHandler(new FishingCatchService(repo, NullLogger<FishingCatchService>.Instance));
        await handler.HandleAsync(new FishingCatchRequest(), session, CancellationToken.None);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
        Assert.Null(repo.LastReplacedContainer);
    }

    [Fact]
    public async Task Step4_GrantsKoiItem_KeepsStateAndBroadcastsActionSort94()
    {
        var (session, pipe, zone, state, repo) = SetUp();
        state.FishingState = 1;
        state.FishingStep = 4;
        state.CatchingFish = true;

        var handler = new FishingCatchHandler(new FishingCatchService(repo, NullLogger<FishingCatchService>.Instance));
        await RunToCompletionAsync(handler.HandleAsync(new FishingCatchRequest(), session, CancellationToken.None),
            zone);

        Assert.NotNull(repo.LastReplacedContainer);
        Assert.Equal(ContainerMatrix.InventoryPage0, repo.LastReplacedContainer!.Value.Container);
        Assert.Single(repo.LastReplacedContainer.Value.Items);
        var grantedItemId = repo.LastReplacedContainer.Value.Items[0].ItemId;
        Assert.Contains(grantedItemId, new[] { 582, 583, 584, 586 });

        Assert.Equal(1, state.FishingState);
        Assert.Equal(4, state.FishingStep);
        Assert.True(state.CatchingFish);
        Assert.Equal(94, state.ActionSort);

        var bytes = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(CatchFrame + ProgressFrame + ActionFrame, bytes.Length);
        Assert.Equal(1, ReadInt32(bytes, 0, 0));
        Assert.Equal(grantedItemId, ReadInt32(bytes, 0, 4));
        Assert.Equal(1, ReadInt32(bytes, CatchFrame, 8));
    }

    [Fact]
    public async Task Step4_InventoryFull_RepliesResult2_ResetsState_NoBroadcast()
    {
        var (session, pipe, zone, state, repo) = SetUp();
        state.FishingState = 1;
        state.FishingStep = 4;
        state.CatchingFish = true;

        var full = ImmutableDictionary.CreateBuilder<byte, ItemStack>();
        for (byte slot = 0; slot <= 63; slot++)
            full[slot] = new ItemStack(1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var builtFull = full.ToImmutable();
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0, builtFull),
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage1, builtFull));
        zone.PostInventoryCommand(new InventoryZoneCommand(10, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var handler = new FishingCatchHandler(new FishingCatchService(repo, NullLogger<FishingCatchService>.Instance));
        await RunToCompletionAsync(handler.HandleAsync(new FishingCatchRequest(), session, CancellationToken.None),
            zone);

        Assert.Null(repo.LastReplacedContainer);
        Assert.Equal(0, state.FishingState);
        Assert.Equal(0, state.FishingStep);

        var bytes = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(CatchFrame, bytes.Length);
        Assert.Equal(2, ReadInt32(bytes, 0, 0));
        Assert.Equal(-1, ReadInt32(bytes, 0, 8));
        Assert.Equal(-1, ReadInt32(bytes, 0, 12));
        Assert.Equal(-1, ReadInt32(bytes, 0, 16));
    }

    [Fact]
    public async Task Step5_Miss_NoItemGrant_OnlyProgressReplyAndBroadcastActionSort95()
    {
        var (session, pipe, zone, state, repo) = SetUp();
        state.FishingState = 1;
        state.FishingStep = 5;
        state.CatchingFish = true;

        var handler = new FishingCatchHandler(new FishingCatchService(repo, NullLogger<FishingCatchService>.Instance));
        await RunToCompletionAsync(handler.HandleAsync(new FishingCatchRequest(), session, CancellationToken.None),
            zone);

        Assert.Null(repo.LastReplacedContainer);
        Assert.Equal(1, state.FishingState);
        Assert.Equal(5, state.FishingStep);
        Assert.Equal(95, state.ActionSort);

        var bytes = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(ProgressFrame + ActionFrame, bytes.Length);
        Assert.Equal(1, ReadInt32(bytes, 0, 8));
    }
}
