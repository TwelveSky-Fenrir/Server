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

public class HolyStoneWarCycleDtmTests
{
    private const short StoneMapId = 38;
    private static readonly HolyStoneWarSite Site = new(StoneMapId, 100f, 100f, 10f, 200f);

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

    private static HolyStoneWarCycle CreateCycle(WorldStateService worldState, ZoneRegistry registry,
        ZoneCenterSiegeState siegeState, int bonusRoll)
    {
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var ingestor = new ZoneCenterBroadcastIngestor(siegeState, registry,
            NullLogger<ZoneCenterBroadcastIngestor>.Instance);
        var gateway =
            new LoggingOnlyHolyStoneCaptureRewardGateway(NullLogger<LoggingOnlyHolyStoneCaptureRewardGateway>.Instance);

        return new HolyStoneWarCycle(worldState, broadcaster, registry, gateway,
            NullLogger<HolyStoneWarCycle>.Instance, Site, new ScriptedRandomSource(bonusRoll), false, ingestor);
    }

    private static void DriveThroughCapture(HolyStoneWarCycle cycle)
    {
        cycle.Tick(HolyStoneWarCycle.NormalCooldown);
        for (var i = 0; i < HolyStoneWarCycle.OpeningCountdownMinutes + 1; i++)
            cycle.Tick(TimeSpan.FromMinutes(1));

        cycle.Tick(TimeSpan.Zero);
        Assert.Equal(HolyStoneWarPhase.ChallengePending, cycle.Phase);

        for (var i = 0; i < HolyStoneWarCycle.ChallengeCountdownMinutes; i++)
            cycle.Tick(TimeSpan.FromMinutes(1));

        Assert.Equal(HolyStoneWarPhase.Cooldown, cycle.Phase);
    }

    [Fact]
    public void Capture_WithNoPreviousHolder_SetsTheNewHolderTribeDtmValueToTheRoll()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(StoneMapId);
        var siegeState = new ZoneCenterSiegeState();
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[StoneMapId].Post(ZoneCommand.Enter(10,
            ZoneTestKit.EnterData(session, StoneMapId, tribe: 1, posX: Site.StoneX, posZ: Site.StoneZ)));
        registry[StoneMapId].Tick(TimeSpan.FromMilliseconds(50));

        var cycle = CreateCycle(worldState, registry, siegeState, bonusRoll: 1);
        DriveThroughCapture(cycle);

        Assert.Equal((byte?)1, worldState.World.Zone038WinTribe);
        Assert.Equal(2, siegeState.GetZone038DtmValue(1));
    }

    [Fact]
    public void Capture_WithPreviousHolder_ClearsThePreviousHolderDtmValue_AndSetsTheNewHolders()
    {
        var worldState = CreateWorldState();
        worldState.SetZone038Winner(2);
        var registry = CreateRegistry(StoneMapId);
        var siegeState = new ZoneCenterSiegeState();
        siegeState.SetZone038DtmValue(2, 5);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[StoneMapId].Post(ZoneCommand.Enter(10,
            ZoneTestKit.EnterData(session, StoneMapId, tribe: 1, posX: Site.StoneX, posZ: Site.StoneZ)));
        registry[StoneMapId].Tick(TimeSpan.FromMilliseconds(50));

        var cycle = CreateCycle(worldState, registry, siegeState, bonusRoll: 2);
        DriveThroughCapture(cycle);

        Assert.Equal((byte?)1, worldState.World.Zone038WinTribe);
        Assert.Equal(0, siegeState.GetZone038DtmValue(2));
        Assert.Equal(3, siegeState.GetZone038DtmValue(1));
    }

    [Fact]
    public void Capture_WithoutIngestor_DoesNotThrow_AndLeavesDtmStateUntouched()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(StoneMapId);
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var gateway =
            new LoggingOnlyHolyStoneCaptureRewardGateway(NullLogger<LoggingOnlyHolyStoneCaptureRewardGateway>.Instance);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[StoneMapId].Post(ZoneCommand.Enter(10,
            ZoneTestKit.EnterData(session, StoneMapId, tribe: 1, posX: Site.StoneX, posZ: Site.StoneZ)));
        registry[StoneMapId].Tick(TimeSpan.FromMilliseconds(50));

        var cycle = new HolyStoneWarCycle(worldState, broadcaster, registry, gateway,
            NullLogger<HolyStoneWarCycle>.Instance, Site, new ScriptedRandomSource(0));

        var exception = Record.Exception(() => DriveThroughCapture(cycle));

        Assert.Null(exception);
        Assert.Equal((byte?)1, worldState.World.Zone038WinTribe);
    }
}
