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

public class HolyStoneWarCycleHostTests
{
    private const short StoneMapId = 38;
    private static readonly HolyStoneWarSite Site = new(StoneMapId, 100f, 100f, 10f, 200f);

    private static ZoneRegistry CreateRegistry(params short[] hostedMaps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options),
            new MovementRules(Options.Create(options)), new DirtyTracker<int>(), NullLogger<Zone>.Instance,
            ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize(hostedMaps);
        return registry;
    }

    private static HolyStoneWarCycleHost CreateHost(short[] hostedMaps, bool holyStoneWarEnabled = true,
        short holyStoneMapId = StoneMapId)
    {
        var options = new GameServerOptions { HolyStoneWarEnabled = holyStoneWarEnabled, HolyStoneMapId = holyStoneMapId };
        var worldState = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        worldState.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        var zoneRegistry = CreateRegistry(hostedMaps);
        var broadcaster = new ZoneEventBroadcaster(worldState, zoneRegistry, NullLogger<ZoneEventBroadcaster>.Instance);
        var gateway =
            new LoggingOnlyHolyStoneCaptureRewardGateway(NullLogger<LoggingOnlyHolyStoneCaptureRewardGateway>.Instance);
        var cycle = new HolyStoneWarCycle(worldState, broadcaster, zoneRegistry, gateway,
            NullLogger<HolyStoneWarCycle>.Instance, Site, new ScriptedRandomSource(0));

        return new HolyStoneWarCycleHost(Options.Create(options), zoneRegistry, cycle,
            NullLogger<HolyStoneWarCycleHost>.Instance);
    }

    [Fact]
    public void MapHostedByThisShard_WithEnabled_IsArmed()
    {
        var host = CreateHost([StoneMapId]);

        Assert.True(host.IsArmed);
    }

    [Fact]
    public void MapHostedByThisShard_WithoutEnabled_IsNotArmed()
    {
        var host = CreateHost([StoneMapId], holyStoneWarEnabled: false);

        Assert.False(host.IsArmed);
    }

    [Fact]
    public void MapNotHostedByThisShard_IsNeverArmed_EvenWithEnabled()
    {
        var host = CreateHost([1]);

        Assert.False(host.IsArmed);
    }
}
