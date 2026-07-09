using System.Buffers.Binary;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Hosting;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests;

// Cross-shard fan-out for the rvr-siege world-event family (Zone049 sub-codes 1-9 + tribe-symbol/alliance
// tSort 38/39/40/42/45/46/47) -- same "constructed directly with fakes, no DI container" idiom as
// SocialCrossShardRelayHostTests/AccountSessionKickPollHostTests.
public class RvrSiegeEventRelayHostTests
{
    private const byte ShardId = 3;

    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options), new MovementRules(Options.Create(options)),
            new DirtyTracker<int>(), NullLogger<Zone>.Instance, ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize(maps);
        return registry;
    }

    private static WorldStateService CreateWorldState()
    {
        var service = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    private static RvrSiegeEventRelayHost CreateHost(FakeRvrSiegeEventRelayRepository relay,
        ZoneCenterBroadcastIngestor ingestor, ZoneEventBroadcaster broadcaster)
    {
        return new RvrSiegeEventRelayHost(new Lazy<ZoneCenterBroadcastIngestor>(ingestor),
            new Lazy<ZoneEventBroadcaster>(broadcaster), relay,
            Options.Create(new GameServerOptions { ShardId = ShardId }),
            NullLogger<RvrSiegeEventRelayHost>.Instance);
    }

    [Fact]
    public async Task PollOnceAsync_QueuedEntry_IsPublishedToTheRepository()
    {
        var registry = CreateRegistry(1);
        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry, NullLogger<ZoneCenterBroadcastIngestor>.Instance);
        var broadcaster = new ZoneEventBroadcaster(CreateWorldState(), registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var relay = new FakeRvrSiegeEventRelayRepository();
        var host = CreateHost(relay, ingestor, broadcaster);

        var entry = new RvrSiegeEventRelayEntry(ShardId, 5, new byte[130]);
        Assert.True(host.Enqueue(entry));

        await host.PollOnceAsync(CancellationToken.None);

        var published = Assert.Single(relay.Published);
        Assert.Equal(entry, published);
    }

    [Fact]
    public async Task PollOnceAsync_DeliveredZone049Row_IsAppliedThroughTheIngestor()
    {
        var registry = CreateRegistry(1);
        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry, NullLogger<ZoneCenterBroadcastIngestor>.Instance);
        var broadcaster = new ZoneEventBroadcaster(CreateWorldState(), registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var relay = new FakeRvrSiegeEventRelayRepository();
        var host = CreateHost(relay, ingestor, broadcaster);

        var data = new byte[130];
        BinaryPrimitives.WriteInt32LittleEndian(data, 4); // slot 4
        relay.NextPoll = [new RvrSiegeEventRelayDto(RelayId: 1, Sort: 2, Data: data)]; // sub-code 2 -> state 1

        await host.PollOnceAsync(CancellationToken.None);

        Assert.Equal(1, state.GetZone049State(4));
    }

    [Fact]
    public async Task PollOnceAsync_DeliveredNonZone049Row_IsAppliedThroughTheBroadcaster()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry, NullLogger<ZoneCenterBroadcastIngestor>.Instance);
        var worldState = CreateWorldState();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var relay = new FakeRvrSiegeEventRelayRepository();
        var host = CreateHost(relay, ingestor, broadcaster);

        var data = new byte[130];
        BinaryPrimitives.WriteInt32LittleEndian(data, 2); // tribe 2 decided Zone038
        relay.NextPoll = [new RvrSiegeEventRelayDto(RelayId: 1, Sort: 38, Data: data)];

        await host.PollOnceAsync(CancellationToken.None);

        // Replayed through ZoneEventBroadcaster.ApplyRelayedEvent -- both the local broadcast AND the
        // WorldStateService mutation are reproduced immediately (see that method's own remarks for why).
        Assert.Equal((byte?)2, worldState.World.Zone038WinTribe);
        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(38, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1)));
    }

    [Fact]
    public async Task PollOnceAsync_PublishFailure_IsIsolated_DoesNotThrow()
    {
        var registry = CreateRegistry(1);
        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry, NullLogger<ZoneCenterBroadcastIngestor>.Instance);
        var broadcaster = new ZoneEventBroadcaster(CreateWorldState(), registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var relay = new FakeRvrSiegeEventRelayRepository { ThrowOnPublish = new InvalidOperationException("boom") };
        var host = CreateHost(relay, ingestor, broadcaster);

        host.Enqueue(new RvrSiegeEventRelayEntry(ShardId, 5, new byte[130]));

        var ex = await Record.ExceptionAsync(() => host.PollOnceAsync(CancellationToken.None).AsTask());
        Assert.Null(ex);
    }

    [Fact]
    public async Task PollOnceAsync_DeliverFailure_IsIsolated_DoesNotThrow()
    {
        var registry = CreateRegistry(1);
        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry, NullLogger<ZoneCenterBroadcastIngestor>.Instance);
        var broadcaster = new ZoneEventBroadcaster(CreateWorldState(), registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var relay = new FakeRvrSiegeEventRelayRepository();
        var host = CreateHost(relay, ingestor, broadcaster);

        // A malformed (too-short) payload makes ApplyRelayedEvent throw -- must not take the whole poll cycle down.
        relay.NextPoll = [new RvrSiegeEventRelayDto(RelayId: 1, Sort: 2, Data: new byte[4])];

        var ex = await Record.ExceptionAsync(() => host.PollOnceAsync(CancellationToken.None).AsTask());
        Assert.Null(ex);
    }
}
