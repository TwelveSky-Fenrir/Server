using System.Buffers.Binary;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class MonsterSymbolAttackWindowNotifySystemTests
{
    private const short HolderMapId = 4;
    private const short OtherMapId = 9;
    private const int LegacyTicksPerMinute = 120;

    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options),
            new MovementRules(Options.Create(options)), new DirtyTracker<int>(), NullLogger<Zone>.Instance,
            ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize(maps);
        return registry;
    }

    private static WorldStateService CreateWorldState()
    {
        var service = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    private static GameServerOptions BuildGameOptions(bool enabled = true, int delayMinutes = 1)
    {
        return new GameServerOptions
        {
            MonsterSymbolAttackNotifyEnabled = enabled,
            MonsterSymbolAttackNotifyDelayMinutes = delayMinutes,
            MonsterSymbolAttackNotifyMapIds = new Dictionary<byte, short> { [0] = HolderMapId, [1] = OtherMapId }
        };
    }

    private static (Zone Zone, FakeDuplexPipe Pipe) EnterOnePlayer(ZoneRegistry registry, short mapId,
        int characterId)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        registry[mapId].Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, mapId)));
        registry[mapId].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        return (registry[mapId], pipe);
    }

    private static Lazy<ZoneEventBroadcaster> LazyBroadcaster(ZoneEventBroadcaster broadcaster)
    {
        return new Lazy<ZoneEventBroadcaster>(() => broadcaster);
    }

    [Fact]
    public void Disabled_NeverBroadcasts_EvenIfHolderAndMapMatch()
    {
        var registry = CreateRegistry(HolderMapId);
        var (zone, pipe) = EnterOnePlayer(registry, HolderMapId, 10);

        var worldState = CreateWorldState();
        worldState.ResolveMonsterSymbol(0);
        var tracker = new MonsterSymbolAttackWindowTracker();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var system = new MonsterSymbolAttackWindowNotifySystem(worldState, tracker, LazyBroadcaster(broadcaster),
            Options.Create(BuildGameOptions(false)));

        system.Simulate(zone, LegacyTicksPerMinute * 10);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void NoCurrentHolder_NeverBroadcasts()
    {
        var registry = CreateRegistry(HolderMapId);
        var (zone, pipe) = EnterOnePlayer(registry, HolderMapId, 10);

        var worldState = CreateWorldState();
        var tracker = new MonsterSymbolAttackWindowTracker();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var system = new MonsterSymbolAttackWindowNotifySystem(worldState, tracker, LazyBroadcaster(broadcaster),
            Options.Create(BuildGameOptions()));

        system.Simulate(zone, LegacyTicksPerMinute * 10);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void NonMatchingZone_NeverBroadcasts_EvenPastDelay()
    {
        var registry = CreateRegistry(HolderMapId, OtherMapId);
        var (_, holderPipe) = EnterOnePlayer(registry, HolderMapId, 10);
        var (otherZone, otherPipe) = EnterOnePlayer(registry, OtherMapId, 20);

        var worldState = CreateWorldState();
        worldState.ResolveMonsterSymbol(0);
        var tracker = new MonsterSymbolAttackWindowTracker();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var system = new MonsterSymbolAttackWindowNotifySystem(worldState, tracker, LazyBroadcaster(broadcaster),
            Options.Create(BuildGameOptions()));

        system.Simulate(otherZone, LegacyTicksPerMinute * 10);

        Assert.Empty(ZoneTestKit.DrainOutbound(otherPipe));
        Assert.Empty(ZoneTestKit.DrainOutbound(holderPipe));
    }

    [Fact]
    public void MissingMapIdEntryForHolder_TreatedAsNoMatch()
    {
        var registry = CreateRegistry(HolderMapId);
        var (zone, pipe) = EnterOnePlayer(registry, HolderMapId, 10);

        var worldState = CreateWorldState();
        worldState.ResolveMonsterSymbol(2);
        var tracker = new MonsterSymbolAttackWindowTracker();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var system = new MonsterSymbolAttackWindowNotifySystem(worldState, tracker, LazyBroadcaster(broadcaster),
            Options.Create(BuildGameOptions()));

        system.Simulate(zone, LegacyTicksPerMinute * 10);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void MatchingZone_ReachesDelay_BroadcastsSort401_NoPayload()
    {
        var registry = CreateRegistry(HolderMapId);
        var (zone, pipe) = EnterOnePlayer(registry, HolderMapId, 10);

        var worldState = CreateWorldState();
        worldState.ResolveMonsterSymbol(0);
        var tracker = new MonsterSymbolAttackWindowTracker();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var system = new MonsterSymbolAttackWindowNotifySystem(worldState, tracker, LazyBroadcaster(broadcaster),
            Options.Create(BuildGameOptions(delayMinutes: 1)));

        system.Simulate(zone, LegacyTicksPerMinute - 1);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));

        system.Simulate(zone, 1);
        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(FrameWriter.FrameSizeOf<ZoneEventInfoResponse>(), frame.Length);
        Assert.Equal(401, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(5)));
    }

    [Fact]
    public void MatchingZone_OnlyNotifiesOncePerHoldingPeriod()
    {
        var registry = CreateRegistry(HolderMapId);
        var (zone, pipe) = EnterOnePlayer(registry, HolderMapId, 10);

        var worldState = CreateWorldState();
        worldState.ResolveMonsterSymbol(0);
        var tracker = new MonsterSymbolAttackWindowTracker();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var system = new MonsterSymbolAttackWindowNotifySystem(worldState, tracker, LazyBroadcaster(broadcaster),
            Options.Create(BuildGameOptions(delayMinutes: 1)));

        system.Simulate(zone, LegacyTicksPerMinute);
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));

        system.Simulate(zone, LegacyTicksPerMinute * 5);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void HolderChanges_ReArmsTheOneShotLatch()
    {
        var registry = CreateRegistry(HolderMapId, OtherMapId);
        var (holderZone, holderPipe) = EnterOnePlayer(registry, HolderMapId, 10);
        var (otherZone, otherPipe) = EnterOnePlayer(registry, OtherMapId, 20);

        var worldState = CreateWorldState();
        worldState.ResolveMonsterSymbol(0);
        var tracker = new MonsterSymbolAttackWindowTracker();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var system = new MonsterSymbolAttackWindowNotifySystem(worldState, tracker, LazyBroadcaster(broadcaster),
            Options.Create(BuildGameOptions(delayMinutes: 1)));

        system.Simulate(holderZone, LegacyTicksPerMinute);
        system.Simulate(otherZone, LegacyTicksPerMinute);
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(holderPipe));

        worldState.ResolveMonsterSymbol(1);
        system.Simulate(holderZone, LegacyTicksPerMinute);
        Assert.Empty(ZoneTestKit.DrainOutbound(holderPipe));

        system.Simulate(otherZone, LegacyTicksPerMinute);
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(otherPipe));
    }
}
