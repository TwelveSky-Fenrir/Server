using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Hosting.World.ZoneWar;
using Fenrir.Application.Game.Tests.Handlers.Tribes;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeVoteElectionCalendarHostTests
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

    private static TribeVoteElection CreateElection()
    {
        var repository = new FakeWorldStateRepository();
        var worldState = new WorldStateService(repository, NullLogger<WorldStateService>.Instance);
        worldState.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(1),
            NullLogger<TribeVoteElection>.Instance);
    }

    private static TribeVoteElectionCalendarHost CreateHost(TribeVoteElection election, short[] hostedMaps,
        bool voteTribeEnabled = true, bool testMode = false, short voteTribeMapId = 37)
    {
        var options = new GameServerOptions
        {
            VoteTribeEnabled = voteTribeEnabled, VoteTribeTestMode = testMode, VoteTribeMapId = voteTribeMapId
        };
        return new TribeVoteElectionCalendarHost(Options.Create(options), CreateRegistry(hostedMaps), election,
            NullLogger<TribeVoteElectionCalendarHost>.Instance);
    }

    [Fact]
    public void MapNotHostedByThisShard_IsNeverArmed_EvenWithVoteTribeEnabled()
    {
        var host = CreateHost(CreateElection(), [1]);

        Assert.False(host.IsArmed);
    }

    [Fact]
    public void MapHostedByThisShard_WithoutVoteTribeEnabled_IsNotArmed()
    {
        var host = CreateHost(CreateElection(), [37], false);

        Assert.False(host.IsArmed);
    }

    [Fact]
    public void MapHostedByThisShard_WithVoteTribeEnabled_IsArmed()
    {
        var host = CreateHost(CreateElection(), [37]);

        Assert.True(host.IsArmed);
    }

    [Fact]
    public async Task NotArmed_TickNeverAdvancesThePhase()
    {
        var election = CreateElection();
        var host = CreateHost(election, [1]);

        await host.TickAsync(new DateTime(2026, 7, 6), CancellationToken.None);

        Assert.Equal(TribeVotePhase.Closed, election.Phase);
    }

    [Fact]
    public async Task Armed_FirstEverTick_OpensRegistration()
    {
        var election = CreateElection();
        var host = CreateHost(election, [37]);

        await host.TickAsync(new DateTime(2026, 7, 6), CancellationToken.None);

        Assert.Equal(TribeVotePhase.Candidacy, election.Phase);
    }

    [Fact]
    public async Task Armed_RepeatedTicksAcrossManyDays_StaysRegistrationOpenForever()
    {
        var election = CreateElection();
        var host = CreateHost(election, [37]);

        await host.TickAsync(new DateTime(2026, 7, 6), CancellationToken.None);
        await host.TickAsync(new DateTime(2026, 7, 7), CancellationToken.None);
        await host.TickAsync(new DateTime(2026, 8, 1), CancellationToken.None);
        await host.TickAsync(new DateTime(2027, 1, 1), CancellationToken.None);

        Assert.Equal(TribeVotePhase.Candidacy, election.Phase);
    }

    [Fact]
    public async Task Armed_TestMode_NeverAdvancesThePhase()
    {
        var election = CreateElection();
        var host = CreateHost(election, [37], testMode: true);

        await host.TickAsync(new DateTime(2026, 7, 6), CancellationToken.None);

        Assert.Equal(TribeVotePhase.Closed, election.Phase);
    }
}
