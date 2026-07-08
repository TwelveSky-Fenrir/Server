using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class FavoredTribeRankBonusLadderServiceTests
{
    private static int OneFrame => FrameWriter.FrameSizeOf<ZoneEventInfoResponse>();

    private static (WorldStateService WorldState, FakeWorldStateRepository Repository) CreateInitializedWorldState()
    {
        var repository = new FakeWorldStateRepository();
        var worldState = new WorldStateService(repository, NullLogger<WorldStateService>.Instance);
        worldState.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return (worldState, repository);
    }

    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options),
            new MovementRules(Options.Create(options)), new DirtyTracker<int>(), NullLogger<Zone>.Instance,
            ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize(maps);
        return registry;
    }

    /// <summary>One connected player in map 1, so a broadcast (if any fires) is directly observable on its pipe.</summary>
    private static (FavoredTribeRankBonusLadderService Service, FakeDuplexPipe Pipe)
        CreateServiceWithOneConnectedZone(WorldStateService worldState)
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe); // discard whatever the enter itself queued

        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var service = new FavoredTribeRankBonusLadderService(worldState, broadcaster,
            NullLogger<FavoredTribeRankBonusLadderService>.Instance);
        return (service, pipe);
    }

    [Fact]
    public async Task FlagAtRestingDefault_TickIfPendingAsync_IsANoOp()
    {
        var (worldState, _) = CreateInitializedWorldState();
        var (service, pipe) = CreateServiceWithOneConnectedZone(worldState);

        await service.TickIfPendingAsync(CancellationToken.None);

        // No-op: FakeWorldStateRepository's own default seed (0) is left completely untouched.
        Assert.Equal(0, worldState.World.UpdateTribePoint);
        Assert.Equal(0, worldState.GetTribe(0).Points);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task FlagAlreadyConsumed_TickIfPendingAsync_IsANoOp()
    {
        var (worldState, _) = CreateInitializedWorldState();
        worldState.SetUpdateTribePointFlag(FavoredTribeRankBonusLadderService.ConsumedFlagValue);
        var (service, pipe) = CreateServiceWithOneConnectedZone(worldState);

        await service.TickIfPendingAsync(CancellationToken.None);

        Assert.Equal(FavoredTribeRankBonusLadderService.ConsumedFlagValue, worldState.World.UpdateTribePoint);
        Assert.Equal(0, worldState.GetTribe(0).Points);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task FlagPending_NoFavoredTribeRecorded_FlipsFlagButWritesNoTotals_AndDoesNotBroadcast()
    {
        var (worldState, _) = CreateInitializedWorldState();
        worldState.SetUpdateTribePointFlag(FavoredTribeRankBonusLadderService.PendingFlagValue);
        // HighTribe left null -- Fenrir's own schema does not seed a default favored tribe (see class remarks).
        var (service, pipe) = CreateServiceWithOneConnectedZone(worldState);

        await service.TickIfPendingAsync(CancellationToken.None);

        Assert.Equal(FavoredTribeRankBonusLadderService.ConsumedFlagValue, worldState.World.UpdateTribePoint);
        // The ladder never ran (no favored tribe) -- every tribe is left at the untouched seed value.
        Assert.Equal(0, worldState.GetTribe(0).Points);
        Assert.Equal(0, worldState.GetTribe(1).Points);
        Assert.Equal(0, worldState.GetTribe(2).Points);
        Assert.Equal(0, worldState.GetTribe(3).Points);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task FlagPending_WithFavoredTribeRecorded_FlipsFlagAppliesLadder_AndBroadcastsSort1234()
    {
        var (worldState, repository) = CreateInitializedWorldState();
        worldState.SetHighTribe(2);
        worldState.SetUpdateTribePointFlag(FavoredTribeRankBonusLadderService.PendingFlagValue);
        var (service, pipe) = CreateServiceWithOneConnectedZone(worldState);

        await service.TickIfPendingAsync(CancellationToken.None);

        Assert.Equal(FavoredTribeRankBonusLadderService.ConsumedFlagValue, worldState.World.UpdateTribePoint);
        Assert.Equal(1000 + 200, worldState.GetTribe(0).Points);
        Assert.Equal(1000 + 300, worldState.GetTribe(1).Points);
        Assert.Equal(1000 + 4000, worldState.GetTribe(2).Points);
        Assert.Equal(1000 + 100, worldState.GetTribe(3).Points);

        // Immediately persisted -- not deferred to the write-behind flush.
        Assert.Contains(repository.TribeUpdateCalls, c => c.TribeId == 0 && c.Points == 1000 + 200);
        Assert.Contains(repository.TribeUpdateCalls, c => c.TribeId == 1 && c.Points == 1000 + 300);
        Assert.Contains(repository.TribeUpdateCalls, c => c.TribeId == 2 && c.Points == 1000 + 4000);
        Assert.Contains(repository.TribeUpdateCalls, c => c.TribeId == 3 && c.Points == 1000 + 100);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame, frame.Length);
        var payload = frame.AsSpan(1);
        Assert.Equal(1234, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(1000 + 200, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        Assert.Equal(1000 + 300, BinaryPrimitives.ReadInt32LittleEndian(payload[8..]));
        Assert.Equal(1000 + 4000, BinaryPrimitives.ReadInt32LittleEndian(payload[12..]));
        Assert.Equal(1000 + 100, BinaryPrimitives.ReadInt32LittleEndian(payload[16..]));
    }

    [Fact]
    public async Task FlagFlipsBeforeApplying_SoARepeatedCallNeverDoubleProcessesTheSameRequest()
    {
        var (worldState, _) = CreateInitializedWorldState();
        worldState.SetHighTribe(0);
        worldState.SetUpdateTribePointFlag(FavoredTribeRankBonusLadderService.PendingFlagValue);
        var (service, pipe) = CreateServiceWithOneConnectedZone(worldState);

        await service.TickIfPendingAsync(CancellationToken.None);
        Assert.Equal(1000 + 4000, worldState.GetTribe(0).Points);
        ZoneTestKit.DrainOutbound(pipe);

        // Simulate an unrelated concurrent overwrite between the two calls -- a second TickIfPendingAsync must
        // not re-apply the ladder since the flag is no longer pending.
        worldState.SetTribePoints(0, 42);
        await service.TickIfPendingAsync(CancellationToken.None);

        Assert.Equal(42, worldState.GetTribe(0).Points);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task EachApplication_IsAFullOverwrite_ResettingEveryTribeToTheBaselineFirst()
    {
        var (worldState, _) = CreateInitializedWorldState();
        worldState.SetTribePoints(1, 99999); // stale standing from an unrelated prior mechanism
        worldState.SetHighTribe(3);
        worldState.SetUpdateTribePointFlag(FavoredTribeRankBonusLadderService.PendingFlagValue);
        var (service, _) = CreateServiceWithOneConnectedZone(worldState);

        await service.TickIfPendingAsync(CancellationToken.None);

        Assert.Equal(1000 + 200, worldState.GetTribe(1).Points);
    }

    [Fact]
    public async Task FlagRewritePersistFails_SkipsTheWholeSequence_FlagStaysPendingForNextPoll()
    {
        var (worldState, repository) = CreateInitializedWorldState();
        worldState.SetHighTribe(1);
        worldState.SetUpdateTribePointFlag(FavoredTribeRankBonusLadderService.PendingFlagValue);
        repository.ThrowOnUpdate = true;
        var (service, pipe) = CreateServiceWithOneConnectedZone(worldState);

        await service.TickIfPendingAsync(CancellationToken.None);

        // Flag write failed -- left exactly as it was (still pending), no totals touched, no broadcast.
        Assert.Equal(FavoredTribeRankBonusLadderService.PendingFlagValue, worldState.World.UpdateTribePoint);
        Assert.Equal(0, worldState.GetTribe(0).Points);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));

        // Next poll retries the whole sequence from scratch, now that the transient failure is gone.
        repository.ThrowOnUpdate = false;
        await service.TickIfPendingAsync(CancellationToken.None);

        Assert.Equal(FavoredTribeRankBonusLadderService.ConsumedFlagValue, worldState.World.UpdateTribePoint);
        Assert.Equal(1000 + 4000, worldState.GetTribe(1).Points);
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task TotalsPersistFails_FlagStaysConsumed_MirrorAlreadyChanged_ButNoBroadcastFires()
    {
        var (worldState, repository) = CreateInitializedWorldState();
        worldState.SetHighTribe(1);
        worldState.SetUpdateTribePointFlag(FavoredTribeRankBonusLadderService.PendingFlagValue);
        repository.ThrowOnUpdateTribeForIds.Add(2); // one tribe's persist fails -> the whole step is a failure
        var (service, pipe) = CreateServiceWithOneConnectedZone(worldState);

        await service.TickIfPendingAsync(CancellationToken.None);

        // Flag already advanced (step 1 succeeded) -- this request is now permanently lost, never retried.
        Assert.Equal(FavoredTribeRankBonusLadderService.ConsumedFlagValue, worldState.World.UpdateTribePoint);

        // In-memory mirror already reflects every new total, including tribe 2's, even though its own DB
        // write failed -- the mirror is never rolled back.
        Assert.Equal(1000 + 300, worldState.GetTribe(0).Points);
        Assert.Equal(1000 + 4000, worldState.GetTribe(1).Points);
        Assert.Equal(1000 + 100, worldState.GetTribe(2).Points);
        Assert.Equal(1000 + 200, worldState.GetTribe(3).Points);
        Assert.DoesNotContain(repository.TribeUpdateCalls, c => c.TribeId == 2);
        Assert.Contains(repository.TribeUpdateCalls, c => c.TribeId == 0);

        // No broadcast -- the persist-and-recompute step reported failure.
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));

        // A later, independent poll never retries this specific lost request (flag stays consumed forever).
        await service.TickIfPendingAsync(CancellationToken.None);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }
}
