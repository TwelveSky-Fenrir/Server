using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeGuardCorridorStateDerivationSystemTests
{
    private const short HubZoneId = 100;
    private static readonly ImmutableArray<short> Tribe0Chain = [1, 2, 3, 4];
    private static readonly ImmutableArray<short> Tribe1Chain = [10, 11, 12, 13];

    private static TribeGuardCorridorCatalog CreateCatalog(bool twoTribes = false,
        ImmutableDictionary<(byte, byte), ImmutableArray<int>>? guardPostSlots = null)
    {
        var builder = ImmutableDictionary.CreateBuilder<byte, TribeGuardCorridorChain>();
        builder[0] = new TribeGuardCorridorChain(Tribe0Chain);
        if (twoTribes)
            builder[1] = new TribeGuardCorridorChain(Tribe1Chain);

        return new TribeGuardCorridorCatalog(HubZoneId, builder.ToImmutable(),
            guardPostSlots ?? ImmutableDictionary<(byte, byte), ImmutableArray<int>>.Empty);
    }

    private static (Zone Zone, TribeGuardCorridorState State) CreateZoneWithSystem(short mapId,
        TribeGuardCorridorCatalog catalog)
    {
        var state = new TribeGuardCorridorState();
        var system = new TribeGuardCorridorStateDerivationSystem(catalog, state);
        var zone = ZoneTestKit.CreateZone(mapId, simulationSystems: [system]);
        return (zone, state);
    }

    private static void SpawnGuard(Zone zone, int serverIndex)
    {
        var template = WorldDataTestRows.Monster(900) with { Life = 100 };
        var entity = MonsterEntity.Create(serverIndex, zone.NextMonsterUniqueNumber(), template, serverIndex,
            0f, 0f, 0f, 15f);
        zone.SpawnMonster(entity);
    }

    [Fact]
    public void UnrelatedZone_NeverTouchesTheStateTable()
    {
        var catalog = CreateCatalog();
        var (zone, state) = CreateZoneWithSystem(9999, catalog);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.False(state.IsOpen(0, 0));
    }

    [Fact]
    public void HubZone_BootTick_ForcesOpenEveryOwnedTribesSegmentZero_RegardlessOfLiveness()
    {
        var catalog = CreateCatalog(true);
        var (zone, state) = CreateZoneWithSystem(HubZoneId, catalog);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(state.IsOpen(0, 0));
        Assert.True(state.IsOpen(1, 0));
    }

    [Fact]
    public void CorridorZone_BootTick_ForcesOpenOnlyItsOwnOwnedSegment()
    {
        var catalog = CreateCatalog();
        var (zone, state) = CreateZoneWithSystem(1, catalog);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(state.IsOpen(0, 1));
        Assert.False(state.IsOpen(0, 2));
    }

    [Fact]
    public void HomeZone_OwnsNoSegment_BootTickIsANoOp()
    {
        var catalog = CreateCatalog();
        var (zone, state) = CreateZoneWithSystem(4, catalog);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.False(state.IsOpen(0, 3));
    }

    [Fact]
    public void SecondTick_LiveGuardPost_ClosesAPreviouslyBootForcedOpenSegment()
    {
        var slots = ImmutableDictionary<(byte, byte), ImmutableArray<int>>.Empty
            .Add((0, 1), ImmutableArray.Create(10, 11, 12, 13, 14));
        var catalog = CreateCatalog(guardPostSlots: slots);
        var (zone, state) = CreateZoneWithSystem(1, catalog);

        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(state.IsOpen(0, 1));

        SpawnGuard(zone, 12);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.False(state.IsOpen(0, 1));
    }

    [Fact]
    public void SecondTick_NoLiveGuardPosts_SegmentStaysOpen()
    {
        var slots = ImmutableDictionary<(byte, byte), ImmutableArray<int>>.Empty
            .Add((0, 1), ImmutableArray.Create(10, 11, 12, 13, 14));
        var catalog = CreateCatalog(guardPostSlots: slots);
        var (zone, state) = CreateZoneWithSystem(1, catalog);

        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(state.IsOpen(0, 1));
    }

    [Fact]
    public void GuardDeath_ReopensTheSegmentOnTheNextEvaluation()
    {
        var slots = ImmutableDictionary<(byte, byte), ImmutableArray<int>>.Empty
            .Add((0, 1), ImmutableArray.Create(10, 11, 12, 13, 14));
        var catalog = CreateCatalog(guardPostSlots: slots);
        var (zone, state) = CreateZoneWithSystem(1, catalog);

        zone.Tick(SimulationClock.LegacyTick);
        SpawnGuard(zone, 12);
        zone.Tick(SimulationClock.LegacyTick);
        Assert.False(state.IsOpen(0, 1));

        zone.TryDamageMonster(12, 10_000, null, out var died, out _);
        Assert.True(died);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(state.IsOpen(0, 1));
    }

    [Fact]
    public void UnconfiguredGuardPostSlots_OwnedSegment_StaysAtWhateverBootLeftIt()
    {
        var catalog = CreateCatalog();
        var (zone, state) = CreateZoneWithSystem(1, catalog);

        zone.Tick(SimulationClock.LegacyTick);
        SpawnGuard(zone, 999);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(state.IsOpen(0, 1));
    }
}
