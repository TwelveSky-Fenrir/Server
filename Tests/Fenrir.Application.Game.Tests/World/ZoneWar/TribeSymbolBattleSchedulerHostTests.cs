using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Hosting.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeSymbolBattleSchedulerHostTests
{
    private static ZoneRegistry CreateRegistry(params short[] hostedMaps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options),
            new MovementRules(Options.Create(options)), new DirtyTracker<int>(), NullLogger<Zone>.Instance,
            ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize(hostedMaps);
        return registry;
    }

    private static TribeSymbolBattleSchedulerHost CreateHost(short[] hostedMaps, bool holyStoneBattleEnabled = true,
        short tribeSymbolBattleMapId = 37)
    {
        var options = new GameServerOptions
        {
            HolyStoneBattleEnabled = holyStoneBattleEnabled, TribeSymbolBattleMapId = tribeSymbolBattleMapId
        };
        var worldState = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        worldState.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        var zoneRegistry = CreateRegistry(hostedMaps);
        var broadcaster = new ZoneEventBroadcaster(worldState, zoneRegistry, NullLogger<ZoneEventBroadcaster>.Instance);
        var scheduler = new TribeSymbolBattleScheduler(worldState, broadcaster,
            NullLogger<TribeSymbolBattleScheduler>.Instance, new HashSet<DayOfWeek>());

        return new TribeSymbolBattleSchedulerHost(Options.Create(options), zoneRegistry, scheduler,
            NullLogger<TribeSymbolBattleSchedulerHost>.Instance);
    }

    [Fact]
    public void MapHostedByThisShard_WithEnabled_IsArmed()
    {
        var host = CreateHost([37]);

        Assert.True(host.IsArmed);
    }

    [Fact]
    public void MapHostedByThisShard_WithoutEnabled_IsNotArmed()
    {
        var host = CreateHost([37], holyStoneBattleEnabled: false);

        Assert.False(host.IsArmed);
    }

    [Fact]
    public void MapNotHostedByThisShard_IsNeverArmed_EvenWithEnabled()
    {
        var host = CreateHost([1]);

        Assert.False(host.IsArmed);
    }
}
