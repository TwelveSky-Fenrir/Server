using System.Buffers.Binary;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Hosting.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class ZoneCenterRegularWarEventSinkTests
{
    private const short SlotZeroMapId = 49;
    private const short SlotOneMapId = 146;

    private static (ZoneCenterRegularWarEventSink Sink, ZoneCenterSiegeState State) CreateSink()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([1]);
        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry,
            NullLogger<ZoneCenterBroadcastIngestor>.Instance);
        var sink = new ZoneCenterRegularWarEventSink(ingestor,
            NullLogger<ZoneCenterRegularWarEventSink>.Instance);
        return (sink, state);
    }

    [Fact]
    public void OnActiveWarStarted_DrivesSubCode4_SettingTheSlotToActiveState3()
    {
        var (sink, state) = CreateSink();

        sink.OnActiveWarStarted(SlotZeroMapId);

        Assert.Equal(3, state.GetZone049State(0));
    }

    [Fact]
    public void OnWarConcluded_DrivesSubCode6_SettingTheSlotToPostWarState4()
    {
        var (sink, state) = CreateSink();

        sink.OnWarConcluded(SlotZeroMapId, RegularWarOutcome.Draw, null,
            ImmutableArray<RegularWarRewardGrant>.Empty, false);

        Assert.Equal(4, state.GetZone049State(0));
    }

    [Fact]
    public void OnAllSessionsShouldDisconnect_DrivesSubCode9_ReturningTheSlotToIdleState0()
    {
        var (sink, state) = CreateSink();

        sink.OnActiveWarStarted(SlotZeroMapId);
        Assert.Equal(3, state.GetZone049State(0));

        sink.OnAllSessionsShouldDisconnect(SlotZeroMapId);

        Assert.Equal(0, state.GetZone049State(0));
    }

    [Fact]
    public void OnCountdownAnnounced_DrivesSubCode1_WithNoStateChange()
    {
        var (sink, state) = CreateSink();

        sink.OnCountdownAnnounced(SlotZeroMapId, 5);

        for (var slot = 0; slot < ZoneCenterSiegeState.Zone049Slots; slot++)
            Assert.Equal(0, state.GetZone049State(slot));
    }

    [Fact]
    public void SlotIsResolvedFromTheCatalog_PerMapId()
    {
        var (sink, state) = CreateSink();

        sink.OnActiveWarStarted(SlotOneMapId);

        Assert.Equal(3, state.GetZone049State(1));
        Assert.Equal(0, state.GetZone049State(0));
    }

    [Fact]
    public void OnSmallestTribeFlaggedAndOnMonstersShouldDespawn_DriveNoIngest()
    {
        var (sink, state) = CreateSink();

        sink.OnSmallestTribeFlagged(SlotZeroMapId, 2);
        sink.OnMonstersShouldDespawn(SlotZeroMapId);

        for (var slot = 0; slot < ZoneCenterSiegeState.Zone049Slots; slot++)
            Assert.Equal(0, state.GetZone049State(slot));
    }

    [Fact]
    public void EventForNonRegularWarMap_IsIgnored_WithoutThrowingOrWritingAnySlot()
    {
        var (sink, state) = CreateSink();

        var exception = Record.Exception(() => sink.OnActiveWarStarted(9999));

        Assert.Null(exception);
        for (var slot = 0; slot < ZoneCenterSiegeState.Zone049Slots; slot++)
            Assert.Equal(0, state.GetZone049State(slot));
    }

    [Fact]
    public void Events_EnqueueForOtherShards_WithTheZone049SubCodeAndSlot()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([1]);
        var state = new ZoneCenterSiegeState();
        var relayQueue = new FakeRvrSiegeEventRelayQueue();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry,
            NullLogger<ZoneCenterBroadcastIngestor>.Instance, relayQueue,
            Options.Create(new GameServerOptions { ShardId = 4 }));
        var sink = new ZoneCenterRegularWarEventSink(ingestor,
            NullLogger<ZoneCenterRegularWarEventSink>.Instance);

        sink.OnActiveWarStarted(SlotOneMapId);

        var entry = Assert.Single(relayQueue.Enqueued);
        Assert.Equal((byte)4, entry.SourceShardId);
        Assert.Equal(4, entry.Sort);
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(entry.Data));
    }
}
