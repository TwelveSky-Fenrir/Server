using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class HolyStoneTerritoryEvictionSweepTests
{
    private static readonly TimeSpan FullCycle =
        HolyStoneTerritoryEvictionSweep.IdleInterval + HolyStoneTerritoryEvictionSweep.GraceInterval;

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

    [Fact]
    public void BeforeAFullCycleElapses_NeverEvictsAnyone()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(39);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[39].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 39, tribe: 3)));
        registry[39].Tick(TimeSpan.FromMilliseconds(50));

        var forcedReturn = new FakeForcedReturnGateway();
        var sweep = new HolyStoneTerritoryEvictionSweep(worldState, registry, [39], forcedReturn,
            NullLogger<HolyStoneTerritoryEvictionSweep>.Instance);

        sweep.Tick(FullCycle - TimeSpan.FromSeconds(1));

        Assert.Empty(forcedReturn.EvictedCharacterIds);
    }

    [Fact]
    public void AfterAFullCycle_NonMatchingTribe_IsEvicted()
    {
        var worldState = CreateWorldState();
        worldState.SetZone038Winner(0); // holder tribe 0
        var registry = CreateRegistry(39);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[39].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 39, tribe: 3)));
        registry[39].Tick(TimeSpan.FromMilliseconds(50));

        var forcedReturn = new FakeForcedReturnGateway();
        var sweep = new HolyStoneTerritoryEvictionSweep(worldState, registry, [39], forcedReturn,
            NullLogger<HolyStoneTerritoryEvictionSweep>.Instance);

        sweep.Tick(FullCycle);

        Assert.Contains(10, forcedReturn.EvictedCharacterIds);
    }

    [Fact]
    public void AfterAFullCycle_MatchingHolderTribe_IsNotEvicted()
    {
        var worldState = CreateWorldState();
        worldState.SetZone038Winner(3);
        var registry = CreateRegistry(39);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[39].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 39, tribe: 3)));
        registry[39].Tick(TimeSpan.FromMilliseconds(50));

        var forcedReturn = new FakeForcedReturnGateway();
        var sweep = new HolyStoneTerritoryEvictionSweep(worldState, registry, [39], forcedReturn,
            NullLogger<HolyStoneTerritoryEvictionSweep>.Instance);

        sweep.Tick(FullCycle);

        Assert.Empty(forcedReturn.EvictedCharacterIds);
    }

    [Fact]
    public void AfterAFullCycle_DeclaredAllyOfHolderTribe_IsNotEvicted()
    {
        var worldState = CreateWorldState();
        worldState.SetZone038Winner(0);
        worldState.SetAllianceOffer(0, 3, true); // tribe 3 is now the declared ally of holder tribe 0
        var registry = CreateRegistry(39);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[39].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 39, tribe: 3)));
        registry[39].Tick(TimeSpan.FromMilliseconds(50));

        var forcedReturn = new FakeForcedReturnGateway();
        var sweep = new HolyStoneTerritoryEvictionSweep(worldState, registry, [39], forcedReturn,
            NullLogger<HolyStoneTerritoryEvictionSweep>.Instance);

        sweep.Tick(FullCycle);

        Assert.Empty(forcedReturn.EvictedCharacterIds);
    }

    [Fact]
    public void NoHolderTribeYet_EverybodyIsEvicted()
    {
        var worldState = CreateWorldState(); // Zone038WinTribe still null
        var registry = CreateRegistry(39);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[39].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 39, tribe: 2)));
        registry[39].Tick(TimeSpan.FromMilliseconds(50));

        var forcedReturn = new FakeForcedReturnGateway();
        var sweep = new HolyStoneTerritoryEvictionSweep(worldState, registry, [39], forcedReturn,
            NullLogger<HolyStoneTerritoryEvictionSweep>.Instance);

        sweep.Tick(FullCycle);

        Assert.Contains(10, forcedReturn.EvictedCharacterIds);
    }

    [Fact]
    public void MapNotInConfiguredTerritorySet_IsNeverSwept()
    {
        var worldState = CreateWorldState();
        worldState.SetZone038Winner(0);
        var registry = CreateRegistry(99); // not a territory map
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[99].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 99, tribe: 3)));
        registry[99].Tick(TimeSpan.FromMilliseconds(50));

        var forcedReturn = new FakeForcedReturnGateway();
        var sweep = new HolyStoneTerritoryEvictionSweep(worldState, registry, [39], forcedReturn,
            NullLogger<HolyStoneTerritoryEvictionSweep>.Instance);

        sweep.Tick(FullCycle);

        Assert.Empty(forcedReturn.EvictedCharacterIds);
    }

    [Fact]
    public void EmptyTerritoryMapSet_NeverEvictsAnyone()
    {
        var worldState = CreateWorldState();
        worldState.SetZone038Winner(0);
        var registry = CreateRegistry(39);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[39].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 39, tribe: 3)));
        registry[39].Tick(TimeSpan.FromMilliseconds(50));

        var forcedReturn = new FakeForcedReturnGateway();
        var sweep = new HolyStoneTerritoryEvictionSweep(worldState, registry, [], forcedReturn,
            NullLogger<HolyStoneTerritoryEvictionSweep>.Instance);

        sweep.Tick(FullCycle * 2);

        Assert.Empty(forcedReturn.EvictedCharacterIds);
    }

    [Fact]
    public void SweepRepeats_OnceEveryFullCycle()
    {
        var worldState = CreateWorldState();
        worldState.SetZone038Winner(0);
        var registry = CreateRegistry(39);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[39].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 39, tribe: 3)));
        registry[39].Tick(TimeSpan.FromMilliseconds(50));

        var forcedReturn = new FakeForcedReturnGateway();
        var sweep = new HolyStoneTerritoryEvictionSweep(worldState, registry, [39], forcedReturn,
            NullLogger<HolyStoneTerritoryEvictionSweep>.Instance);

        sweep.Tick(FullCycle);
        Assert.Equal(1, forcedReturn.EvictedCharacterIds.Count(id => id == 10));

        sweep.Tick(FullCycle);
        Assert.Equal(2, forcedReturn.EvictedCharacterIds.Count(id => id == 10));
    }

    private sealed class FakeForcedReturnGateway : IHolyStoneForcedReturnGateway
    {
        public List<int> EvictedCharacterIds { get; } = [];

        public void ForceReturnToSafeLocation(Zone zone, PlayerRuntimeState player)
        {
            EvictedCharacterIds.Add(player.CharacterId);
        }
    }
}
