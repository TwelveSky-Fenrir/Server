using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Tribes;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

public class TribePopulationServiceTests
{
    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options),
            new MovementRules(Options.Create(options)), new DirtyTracker<int>(), NullLogger<Zone>.Instance,
            ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize(maps);
        return registry;
    }

    [Fact]
    public void GetConnectedUserCounts_ScopesToRequestersOwnZoneOnly()
    {
        var registry = CreateRegistry(1, 2);

        var (sessionA, _) = ZoneTestKit.CreateSession(1);
        var (sessionB, _) = ZoneTestKit.CreateSession(2);
        var (sessionC, _) = ZoneTestKit.CreateSession(3);

        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1, tribe: 0)));
        registry[1].Post(ZoneCommand.Enter(11, ZoneTestKit.EnterData(sessionB, 1, tribe: 0)));
        registry[2].Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionC, 2, tribe: 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        registry[2].Tick(TimeSpan.FromMilliseconds(50));

        var service = new TribePopulationService(NullLogger<TribePopulationService>.Instance);

        // Requester on map 1 only sees map 1's population, not map 2's -- matching the legacy
        // one-process-per-map semantics (TribePopulation behavior contract).
        var counts = service.GetConnectedUserCounts(registry[1]);

        Assert.Equal(4, counts.Count);
        Assert.Equal(2, counts[0]);
        Assert.Equal(0, counts[1]);
        Assert.Equal(0, counts[2]);
        Assert.Equal(0, counts[3]);

        var countsOnMap2 = service.GetConnectedUserCounts(registry[2]);

        Assert.Equal(0, countsOnMap2[0]);
        Assert.Equal(1, countsOnMap2[1]);
    }

    [Fact]
    public void GetConnectedUserCounts_NoPlayersInZone_AllZero()
    {
        var registry = CreateRegistry(1);
        var service = new TribePopulationService(NullLogger<TribePopulationService>.Instance);

        var counts = service.GetConnectedUserCounts(registry[1]);

        Assert.Equal(4, counts.Count);
        for (var i = 0; i < 4; i++)
            Assert.Equal(0, counts[i]);
    }
}
