using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneRegistryTests
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
    public void BuildsOneZonePerConfiguredMap_WithMatchingMapIds()
    {
        var registry = CreateRegistry(1, 2, 33);

        Assert.Equal(3, registry.Zones.Length);
        Assert.Equal(new short[] { 1, 2, 33 }, registry.Zones.Select(z => z.MapId).Order().ToArray());
    }

    [Fact]
    public void TryGet_HostedMap_ReturnsItsZone()
    {
        var registry = CreateRegistry(1, 2);

        Assert.True(registry.TryGet(2, out var zone));
        Assert.Equal(2, zone!.MapId);
        Assert.Same(zone, registry[2]);
    }

    // must return false, not throw -- a character persisted on a map this shard doesn't host relies on this
    [Fact]
    public void TryGet_UnhostedMap_ReturnsFalse()
    {
        var registry = CreateRegistry(1, 2);

        Assert.False(registry.TryGet(99, out var zone));
        Assert.Null(zone);
    }

    [Fact]
    public void TotalPlayerCount_SumsAcrossZones()
    {
        var registry = CreateRegistry(1, 2);
        var (sessionA, _) = ZoneTestKit.CreateSession(1);
        var (sessionB, _) = ZoneTestKit.CreateSession(2);

        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1)));
        registry[2].Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionB, 2)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        registry[2].Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(2, registry.TotalPlayerCount);
    }

    [Fact]
    public void TryGetPlayer_FindsCharacterInWhicheverZoneHostsIt()
    {
        var registry = CreateRegistry(1, 2);
        var (session, _) = ZoneTestKit.CreateSession(1);

        registry[2].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 2)));
        registry[2].Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(registry.TryGetPlayer(10, out var state));
        Assert.Equal((short)2, state!.MapId);
        Assert.False(registry.TryGetPlayer(999, out _));
    }
}
