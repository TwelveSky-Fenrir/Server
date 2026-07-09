using System.Buffers.Binary;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers rvr-siege's cross-shard relay leg on <see cref="ZoneEventBroadcaster" />: the seven producer
///     methods (tSort 38/39/40/42/45/46/47) enqueueing onto <see cref="IRvrSiegeEventRelayQueue" /> after their
///     own unchanged same-shard work, and <see cref="ZoneEventBroadcaster.ApplyRelayedEvent" /> reproducing the
///     local broadcast plus reactive guard/symbol resummon for a row this shard did NOT originate.
/// </summary>
public class ZoneEventBroadcasterRelayTests
{
    private const byte MainType = 5;
    private const byte SpecialType = 7;
    private static int OneFrame => FrameWriter.FrameSizeOf<ZoneEventInfoResponse>();

    private static ZoneRegistry CreateRegistry(WorldDataCache cache, params short[] maps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options), new MovementRules(Options.Create(options)),
            new DirtyTracker<int>(), NullLogger<Zone>.Instance, cache, []);
        registry.Initialize(maps);
        return registry;
    }

    private static WorldDataCache CacheWithGuardTemplate()
    {
        var monster = WorldDataTestRows.Monster(900) with { Type = MainType, SpecialType = SpecialType, Life = 100 };
        var rows = WorldDataTestRows.MinimalRows() with { Monsters = [monster] };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static GuardPostDefinition Post(short mapId, byte tribeId)
    {
        var slots = ImmutableArray.Create(new GuardSlotCoordinate(0f, 0f, 0f, 0));
        return new GuardPostDefinition(mapId, tribeId, MainType, SpecialType, slots);
    }

    private static WorldStateService CreateWorldState()
    {
        var service = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    [Fact]
    public void AnnounceZone038Winner_EnqueuesSort38_WithTheSameTribeIdPayload()
    {
        var registry = CreateRegistry(ZoneTestKit.EmptyWorldData(), 1);
        var worldState = CreateWorldState();
        var relayQueue = new FakeRvrSiegeEventRelayQueue();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance,
            relayQueue: relayQueue, gameOptions: Options.Create(new GameServerOptions { ShardId = 9 }));

        broadcaster.AnnounceZone038Winner(2);

        var entry = Assert.Single(relayQueue.Enqueued);
        Assert.Equal((byte)9, entry.SourceShardId);
        Assert.Equal(38, entry.Sort);
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(entry.Data));
    }

    [Fact]
    public void AnnounceTribeSymbolBattleStarted_EnqueuesSort40()
    {
        var registry = CreateRegistry(ZoneTestKit.EmptyWorldData(), 1);
        var worldState = CreateWorldState();
        var relayQueue = new FakeRvrSiegeEventRelayQueue();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance,
            relayQueue: relayQueue, gameOptions: Options.Create(new GameServerOptions { ShardId = 9 }));

        broadcaster.AnnounceTribeSymbolBattleStarted();

        var entry = Assert.Single(relayQueue.Enqueued);
        Assert.Equal(40, entry.Sort);
    }

    [Fact]
    public void AnnounceAllianceOffer_EnqueuesSort46_WithBothTribesAndAcceptedFlag()
    {
        var registry = CreateRegistry(ZoneTestKit.EmptyWorldData(), 1);
        var worldState = CreateWorldState();
        var relayQueue = new FakeRvrSiegeEventRelayQueue();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance,
            relayQueue: relayQueue, gameOptions: Options.Create(new GameServerOptions { ShardId = 9 }));

        broadcaster.AnnounceAllianceOffer(0, 1, true);

        var entry = Assert.Single(relayQueue.Enqueued);
        Assert.Equal(46, entry.Sort);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(entry.Data));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(entry.Data.AsSpan(4)));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(entry.Data.AsSpan(8)));
    }

    [Fact]
    public void Announce_WithNoRelayQueueConfigured_NeverThrows()
    {
        var registry = CreateRegistry(ZoneTestKit.EmptyWorldData(), 1);
        var worldState = CreateWorldState();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);

        var exception = Record.Exception(() => broadcaster.AnnounceZone038Winner(1));

        Assert.Null(exception);
    }

    [Fact]
    public void
        ApplyRelayedEvent_Sort38_ReplaysTheWorldStateMutation_BroadcastsLocally_AndForcesTheZone038WinnerGuardResummon()
    {
        var cache = CacheWithGuardTemplate();
        var registry = CreateRegistry(cache, TribeGuardSpawner.Zone038MapId);
        var zone = registry[TribeGuardSpawner.Zone038MapId];
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, TribeGuardSpawner.Zone038MapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var worldState = CreateWorldState();
        var catalog = new GuardPostCatalog([], [Post(TribeGuardSpawner.Zone038MapId, 1)]);
        var guardSpawner = new TribeGuardSpawner(cache, catalog, worldState);
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance,
            guardSpawner);

        guardSpawner.Simulate(zone, 1); // burn the boot pass, still a no-op (no winner recorded)
        Assert.Equal(0, zone.MonsterCount);

        var data = new byte[130];
        BinaryPrimitives.WriteInt32LittleEndian(data, 1);
        broadcaster.ApplyRelayedEvent(38, data);

        // The relay replay must reproduce the WorldStateService mutation immediately (not wait on
        // WorldStateService's own separate DB reconcile poll) -- ForceZone038WinnerResummon's own effect
        // depends on reading Zone038WinTribe back out on this very call, so a skipped/lagged mutation here
        // would silently leave the guard resummon a no-op.
        Assert.Equal((byte?)1, worldState.World.Zone038WinTribe);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame, frame.Length);
        Assert.Equal(38, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1)));

        guardSpawner.Simulate(zone, 1); // consumes the forced flag ApplyRelayedEvent just set
        Assert.Equal(1, zone.MonsterCount);
    }

    [Fact]
    public void ApplyRelayedEvent_Sort42_ReplaysTheSymbolResolution_NoReactiveGuardOrSymbolEffect_ButBroadcastsLocally()
    {
        var registry = CreateRegistry(ZoneTestKit.EmptyWorldData(), 1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var worldState = CreateWorldState();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);

        var data = new byte[130];
        BinaryPrimitives.WriteInt32LittleEndian(data, 2);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 1);
        broadcaster.ApplyRelayedEvent(42, data);

        // Slot 2 contested, tribe 1 wins it -- slot 2 loses its own symbol.
        Assert.False(worldState.GetTribe(2).HasSymbol);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(42, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1)));
    }

    [Fact]
    public void ApplyRelayedEvent_WrongPayloadLength_Throws()
    {
        var registry = CreateRegistry(ZoneTestKit.EmptyWorldData(), 1);
        var worldState = CreateWorldState();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);

        Assert.Throws<ArgumentException>(() => broadcaster.ApplyRelayedEvent(38, new byte[4]));
    }
}
