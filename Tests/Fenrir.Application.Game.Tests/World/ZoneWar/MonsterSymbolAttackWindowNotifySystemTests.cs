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

/// <summary>
///     Covers <see cref="MonsterSymbolAttackWindowNotifySystem" />: the per-tick gate on the current
///     monster-symbol holder, the holder-tribe-to-mapped-instance filter, and the one-shot sort-401 broadcast.
/// </summary>
public class MonsterSymbolAttackWindowNotifySystemTests
{
    private const short HolderMapId = 4; // mapped to holder tribe 0 in every test's options
    private const short OtherMapId = 9; // mapped to holder tribe 1, never the current holder below
    private const int LegacyTicksPerMinute = 120; // Server/Header/function.h:1643-1652

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

    [Fact]
    public void Disabled_NeverBroadcasts_EvenIfHolderAndMapMatch()
    {
        var registry = CreateRegistry(HolderMapId);
        var (zone, pipe) = EnterOnePlayer(registry, HolderMapId, 10);

        var worldState = CreateWorldState();
        worldState.ResolveMonsterSymbol(0);
        var tracker = new MonsterSymbolAttackWindowTracker();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var system = new MonsterSymbolAttackWindowNotifySystem(worldState, tracker, broadcaster,
            Options.Create(BuildGameOptions(enabled: false)));

        system.Simulate(zone, LegacyTicksPerMinute * 10);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void NoCurrentHolder_NeverBroadcasts()
    {
        var registry = CreateRegistry(HolderMapId);
        var (zone, pipe) = EnterOnePlayer(registry, HolderMapId, 10);

        var worldState = CreateWorldState(); // MonsterSymbol never resolved -- null
        var tracker = new MonsterSymbolAttackWindowTracker();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var system = new MonsterSymbolAttackWindowNotifySystem(worldState, tracker, broadcaster,
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
        worldState.ResolveMonsterSymbol(0); // holder tribe 0 -> mapped to HolderMapId, not OtherMapId
        var tracker = new MonsterSymbolAttackWindowTracker();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var system = new MonsterSymbolAttackWindowNotifySystem(worldState, tracker, broadcaster,
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
        worldState.ResolveMonsterSymbol(2); // tribe 2 has no configured mapping in BuildGameOptions (only 0/1)
        var tracker = new MonsterSymbolAttackWindowTracker();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var system = new MonsterSymbolAttackWindowNotifySystem(worldState, tracker, broadcaster,
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
        var system = new MonsterSymbolAttackWindowNotifySystem(worldState, tracker, broadcaster,
            Options.Create(BuildGameOptions(delayMinutes: 1)));

        system.Simulate(zone, LegacyTicksPerMinute - 1); // one tick short of the 1-minute delay
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));

        system.Simulate(zone, 1); // crosses the delay on this exact tick
        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(FrameWriter.FrameSizeOf<ZoneEventInfoResponse>(), frame.Length);
        Assert.Equal(401, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1)));
        // No payload beyond the sort itself -- the rest of the 130-byte data block stays zero.
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
        var system = new MonsterSymbolAttackWindowNotifySystem(worldState, tracker, broadcaster,
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
        worldState.ResolveMonsterSymbol(0); // tribe 0 -> HolderMapId
        var tracker = new MonsterSymbolAttackWindowTracker();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var system = new MonsterSymbolAttackWindowNotifySystem(worldState, tracker, broadcaster,
            Options.Create(BuildGameOptions(delayMinutes: 1)));

        system.Simulate(holderZone, LegacyTicksPerMinute);
        system.Simulate(otherZone, LegacyTicksPerMinute); // never matches -- no-op every call
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(holderPipe));

        worldState.ResolveMonsterSymbol(1); // tribe 1 -> OtherMapId
        system.Simulate(holderZone, LegacyTicksPerMinute); // no longer the holder's map -- no broadcast
        Assert.Empty(ZoneTestKit.DrainOutbound(holderPipe));

        system.Simulate(otherZone, LegacyTicksPerMinute); // now matches -- fresh holding period, notifies again
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(otherPipe));
    }
}
