using Fenrir.Application.Game.Handlers.Tribes.Services;
using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
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
    public void GetConnectedUserCounts_CountsEveryTribeAcrossEveryZone()
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

        var service = new TribePopulationService(registry);

        var counts = service.GetConnectedUserCounts();

        Assert.Equal(4, counts.Count);
        Assert.Equal(2, counts[0]);
        Assert.Equal(1, counts[1]);
        Assert.Equal(0, counts[2]);
        Assert.Equal(0, counts[3]);
    }

    [Fact]
    public void GetConnectedUserCounts_NoPlayersAnywhere_AllZero()
    {
        var registry = CreateRegistry(1);
        var service = new TribePopulationService(registry);

        var counts = service.GetConnectedUserCounts();

        Assert.Equal(4, counts.Count);
        for (var i = 0; i < 4; i++)
            Assert.Equal(0, counts[i]);
    }
}
