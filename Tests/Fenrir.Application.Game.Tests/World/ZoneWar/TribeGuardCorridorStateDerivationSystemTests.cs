using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="TribeGuardCorridorStateDerivationSystem" /> (<c>MyGame::ProcessForGuardState</c>)
///     against a synthetic two-tribe corridor -- the real sixteen-zone/hub table is not reproduced anywhere in
///     this codebase yet (see <see cref="TribeGuardCorridorCatalog" />'s own remarks).
/// </summary>
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
        var catalog = CreateCatalog(twoTribes: true);
        var (zone, state) = CreateZoneWithSystem(HubZoneId, catalog);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(state.IsOpen(0, 0));
        Assert.True(state.IsOpen(1, 0));
    }

    [Fact]
    public void CorridorZone_BootTick_ForcesOpenOnlyItsOwnOwnedSegment()
    {
        var catalog = CreateCatalog();
        var (zone, state) = CreateZoneWithSystem(1, catalog); // tribe 0's chain[0] -- owns segment 1

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(state.IsOpen(0, 1));
        Assert.False(state.IsOpen(0, 2)); // not this zone's own segment to own
    }

    [Fact]
    public void HomeZone_OwnsNoSegment_BootTickIsANoOp()
    {
        var catalog = CreateCatalog();
        var (zone, state) = CreateZoneWithSystem(4, catalog); // tribe 0's home zone (chain[3])

        zone.Tick(SimulationClock.LegacyTick);

        Assert.False(state.IsOpen(0, 3));
    }

    [Fact]
    public void SecondTick_LiveGuardPost_ClosesAPreviouslyBootForcedOpenSegment()
    {
        var slots = ImmutableDictionary<(byte, byte), ImmutableArray<int>>.Empty
            .Add(((byte)0, (byte)1), ImmutableArray.Create(10, 11, 12, 13, 14));
        var catalog = CreateCatalog(guardPostSlots: slots);
        var (zone, state) = CreateZoneWithSystem(1, catalog); // owns segment (0,1)

        zone.Tick(SimulationClock.LegacyTick); // tick 1 -- boot pass, forced open
        Assert.True(state.IsOpen(0, 1));

        SpawnGuard(zone, 12); // one of the five configured guard-post slots
        zone.Tick(SimulationClock.LegacyTick); // tick 2 -- live scan notices the guard alive

        Assert.False(state.IsOpen(0, 1));
    }

    [Fact]
    public void SecondTick_NoLiveGuardPosts_SegmentStaysOpen()
    {
        var slots = ImmutableDictionary<(byte, byte), ImmutableArray<int>>.Empty
            .Add(((byte)0, (byte)1), ImmutableArray.Create(10, 11, 12, 13, 14));
        var catalog = CreateCatalog(guardPostSlots: slots);
        var (zone, state) = CreateZoneWithSystem(1, catalog);

        zone.Tick(SimulationClock.LegacyTick); // boot -- forced open
        zone.Tick(SimulationClock.LegacyTick); // live scan -- nobody alive, stays open

        Assert.True(state.IsOpen(0, 1));
    }

    [Fact]
    public void GuardDeath_ReopensTheSegmentOnTheNextEvaluation()
    {
        var slots = ImmutableDictionary<(byte, byte), ImmutableArray<int>>.Empty
            .Add(((byte)0, (byte)1), ImmutableArray.Create(10, 11, 12, 13, 14));
        var catalog = CreateCatalog(guardPostSlots: slots);
        var (zone, state) = CreateZoneWithSystem(1, catalog);

        zone.Tick(SimulationClock.LegacyTick); // tick 1 -- boot, forced open
        SpawnGuard(zone, 12);
        zone.Tick(SimulationClock.LegacyTick); // tick 2 -- guard alive, closes
        Assert.False(state.IsOpen(0, 1));

        zone.TryDamageMonster(12, 10_000, null, out var died, out _);
        Assert.True(died);
        zone.Tick(SimulationClock.LegacyTick); // tick 3 -- no guards alive anymore, reopens

        Assert.True(state.IsOpen(0, 1));
    }

    [Fact]
    public void UnconfiguredGuardPostSlots_OwnedSegment_StaysAtWhateverBootLeftIt()
    {
        // Segment is owned by this zone (so the boot pass forces it open), but no guard-post slots are
        // configured for it yet -- EvaluateSegment must skip the scan (documented catalog gap), leaving the
        // boot-forced state alone rather than guessing.
        var catalog = CreateCatalog(); // no guardPostSlots at all
        var (zone, state) = CreateZoneWithSystem(1, catalog);

        zone.Tick(SimulationClock.LegacyTick); // boot -- forced open
        SpawnGuard(zone, 999); // would-be guard at an arbitrary, unconfigured index
        zone.Tick(SimulationClock.LegacyTick); // live scan -- no configured slots, no-op

        Assert.True(state.IsOpen(0, 1));
    }
}
