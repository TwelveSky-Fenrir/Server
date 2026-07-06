using System.Buffers.Binary;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers the forced-resummon wiring <see cref="ZoneEventBroadcaster" /> adds on top of
///     <see cref="TribeGuardSpawner" />/<see cref="TribeSymbolSpawner" /> (the HSB broadcast relay cases
///     39/40 and the zone038-conclusion call, see that class's own remarks) -- proves the production entry
///     points actually reach the spawners, not just the spawners' own direct API. Every test burns through
///     <see cref="TribeGuardSpawner" />'s own boot-time forced pass first, so the later assertions can only be
///     explained by the broadcaster's own forced-resummon call, never by boot alone.
/// </summary>
public class ZoneEventBroadcasterGuardSymbolTests
{
    private const byte MainType = 5;
    private const byte SpecialType = 7;

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

    private static ZoneRegistry CreateRegistry(WorldDataCache cache, params short[] maps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options), new MovementRules(Options.Create(options)),
            new DirtyTracker<int>(), NullLogger<Zone>.Instance, cache, []);
        registry.Initialize(maps);
        return registry;
    }

    [Fact]
    public void AnnounceZone038Winner_ForcesTheZone038WinnerGuardPool_ToResummonOnItsMap()
    {
        var cache = CacheWithGuardTemplate();
        var registry = CreateRegistry(cache, TribeGuardSpawner.Zone038MapId);
        var zone = registry[TribeGuardSpawner.Zone038MapId];
        var worldState = CreateWorldState();
        var catalog = new GuardPostCatalog([], [Post(TribeGuardSpawner.Zone038MapId, 1)]);
        var guardSpawner = new TribeGuardSpawner(cache, catalog, worldState);
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance,
            guardSpawner);

        // Burn the boot pass first -- no winner recorded yet, so the zone038-winner pool stays a no-op.
        guardSpawner.Simulate(zone, 1);
        Assert.Equal(0, zone.MonsterCount);

        broadcaster.AnnounceZone038Winner(1);
        guardSpawner.Simulate(zone, 1); // consumes the forced flag AnnounceZone038Winner just set

        Assert.Equal(1, zone.MonsterCount);
    }

    [Fact]
    public void AnnounceTribeSymbolBattleCountdown_BroadcastsSort39_AndForcesOrdinaryGuardResummon()
    {
        var cache = CacheWithGuardTemplate();
        var registry = CreateRegistry(cache, 2);
        var zone = registry[2];
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 2)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var worldState = CreateWorldState();
        var catalog = new GuardPostCatalog([Post(2, 0)], []);
        var guardSpawner = new TribeGuardSpawner(cache, catalog);
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance,
            guardSpawner);

        // Boot pass unconditionally pops this post (no gate on it) -- kill the guard back off so the later
        // resummon is the only thing that can bring the count back to 1.
        guardSpawner.Simulate(zone, 1);
        Assert.Equal(1, zone.MonsterCount);
        var serverIndex = zone.MonstersSnapshot.Single().ServerIndex;
        zone.TryDamageMonster(serverIndex, 10_000, null, out _, out _);
        Assert.Equal(0, zone.MonsterCount);
        ZoneTestKit.DrainOutbound(
            pipe); // discard the kill/death broadcast so the frame below is only the sort-39 announce

        broadcaster.AnnounceTribeSymbolBattleCountdown();

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(39, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1)));

        guardSpawner.Simulate(zone, 1); // consumes the forced flag -- bypasses the (still cooling) respawn timer
        Assert.Equal(1, zone.MonsterCount);
    }

    [Fact]
    public void AnnounceTribeSymbolBattleStarted_EvaluatesSymbolsImmediately_AndForcesOrdinaryGuardResummon()
    {
        var cache = CacheWithGuardTemplate();
        var registry = CreateRegistry(cache, 2);
        var zone = registry[2];
        var worldState = CreateWorldState();
        var guardCatalog = new GuardPostCatalog([Post(2, 0)], []);
        var guardSpawner = new TribeGuardSpawner(cache, guardCatalog);

        var symbolMonster = WorldDataTestRows.Monster(601) with { SpecialType = 11, Life = 50 };
        var symbolCache = WorldDataCacheBuilder.Build(
            WorldDataTestRows.MinimalRows() with { Monsters = [symbolMonster] }).Cache;
        var symbolCatalog = new TribeSymbolCatalog(
            ImmutableDictionary<byte, ImmutableDictionary<byte, TribeSymbolPlacement>>.Empty.Add(0,
                ImmutableDictionary<byte, TribeSymbolPlacement>.Empty.Add(0, new TribeSymbolPlacement(2, 0f, 0f, 0f))));
        var symbolSpawner = new TribeSymbolSpawner(symbolCache, symbolCatalog, worldState);

        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance,
            guardSpawner, symbolSpawner);

        // Burn the guard's own boot pass and kill it back off, isolating the resummon this test cares about.
        guardSpawner.Simulate(zone, 1);
        var serverIndex = zone.MonstersSnapshot.Single().ServerIndex;
        zone.TryDamageMonster(serverIndex, 10_000, null, out _, out _);
        Assert.Equal(0, zone.MonsterCount);

        broadcaster.AnnounceTribeSymbolBattleStarted();

        // The symbol evaluation runs synchronously inside the call itself.
        Assert.Equal(1, zone.MonsterCount);

        guardSpawner.Simulate(zone, 1); // consumes the forced ordinary-pool flag the same call also set
        Assert.Equal(2, zone.MonsterCount); // the symbol above, plus the now-resummoned guard
    }
}
