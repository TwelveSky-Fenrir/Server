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

public class AllianceDiplomacyCeremonyHostTests
{
    private const short AllianceMapId = 37;

    private static ZoneRegistry CreateRegistry(params short[] hostedMaps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options),
            new MovementRules(Options.Create(options)), new DirtyTracker<int>(), NullLogger<Zone>.Instance,
            ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize(hostedMaps);
        return registry;
    }

    private static (AllianceDiplomacyCeremonyHost Host, ZoneRegistry Registry) CreateHost(short[] hostedMaps,
        bool allianceTribeEnabled = true, short allianceTribeMapId = AllianceMapId)
    {
        var options = new GameServerOptions
        {
            AllianceTribeEnabled = allianceTribeEnabled, AllianceTribeMapId = allianceTribeMapId
        };
        var worldState = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        worldState.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        var zoneRegistry = CreateRegistry(hostedMaps);
        var broadcaster = new ZoneEventBroadcaster(worldState, zoneRegistry, NullLogger<ZoneEventBroadcaster>.Instance);
        var cooldowns = new AllianceCooldownTracker();
        var ceremony = new AllianceDiplomacyCeremony(worldState, cooldowns, broadcaster,
            NullLogger<AllianceDiplomacyCeremony>.Instance,
            AllianceDiplomacyCeremony.NegotiationConfirmationDurationRawTicks,
            AllianceDiplomacyCeremony.NegotiationConfirmationDurationRawTicks);

        var host = new AllianceDiplomacyCeremonyHost(Options.Create(options), zoneRegistry, ceremony,
            NullLogger<AllianceDiplomacyCeremonyHost>.Instance);

        return (host, zoneRegistry);
    }

    [Fact]
    public void MapHostedByThisShard_WithEnabled_IsArmed()
    {
        var (host, _) = CreateHost([AllianceMapId]);

        Assert.True(host.IsArmed);
    }

    [Fact]
    public void MapHostedByThisShard_WithoutEnabled_IsNotArmed()
    {
        var (host, _) = CreateHost([AllianceMapId], false);

        Assert.False(host.IsArmed);
    }

    [Fact]
    public void MapNotHostedByThisShard_IsNeverArmed_EvenWithEnabled()
    {
        var (host, _) = CreateHost([1]);

        Assert.False(host.IsArmed);
    }

    [Fact]
    public void Tick_NoZoneHosted_DoesNotThrow()
    {
        var (host, _) = CreateHost([1], allianceTribeMapId: AllianceMapId);

        var exception = Record.Exception(host.Tick);

        Assert.Null(exception);
    }

    [Fact]
    public void Tick_ZoneHostedButNoOccupants_StaysIdle_DoesNotThrow()
    {
        var (host, _) = CreateHost([AllianceMapId]);

        var exception = Record.Exception(host.Tick);

        Assert.Null(exception);
    }
}
