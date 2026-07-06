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
    private static ZoneRegistry CreateRegistry()
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options),
            new MovementRules(Options.Create(options)), new DirtyTracker<int>(), NullLogger<Zone>.Instance,
            ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize([1]);
        return registry;
    }

    private static TribeVoteElection CreateElection()
    {
        var repository = new FakeWorldStateRepository();
        var worldState = new WorldStateService(repository, NullLogger<WorldStateService>.Instance);
        worldState.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(),
            NullLogger<TribeVoteElection>.Instance);
    }

    private static TribeVoteElectionCalendarHost CreateHost(TribeVoteElection election, byte shardId = 37,
        bool voteTribeEnabled = true, bool testMode = false)
    {
        var options = new GameServerOptions
        {
            ShardId = shardId, VoteTribeEnabled = voteTribeEnabled, VoteTribeTestMode = testMode
        };
        return new TribeVoteElectionCalendarHost(Options.Create(options), election,
            NullLogger<TribeVoteElectionCalendarHost>.Instance);
    }

    [Fact]
    public void NotServerNumber37_IsNeverArmed_EvenWithVoteTribeEnabled()
    {
        var host = CreateHost(CreateElection(), shardId: 1, voteTribeEnabled: true);

        Assert.False(host.IsArmed);
    }

    [Fact]
    public void ServerNumber37_WithoutVoteTribeEnabled_IsNotArmed()
    {
        var host = CreateHost(CreateElection(), shardId: 37, voteTribeEnabled: false);

        Assert.False(host.IsArmed);
    }

    [Fact]
    public void ServerNumber37_WithVoteTribeEnabled_IsArmed()
    {
        var host = CreateHost(CreateElection(), shardId: 37, voteTribeEnabled: true);

        Assert.True(host.IsArmed);
    }

    [Fact]
    public async Task NotArmed_TickNeverAdvancesThePhase()
    {
        var election = CreateElection();
        var host = CreateHost(election, shardId: 1, voteTribeEnabled: true);

        await host.TickAsync(new DateTime(2026, 7, 6), CancellationToken.None);

        Assert.Equal(TribeVotePhase.Closed, election.Phase);
    }

    [Fact]
    public async Task Armed_FirstEverTick_OpensRegistration()
    {
        var election = CreateElection();
        var host = CreateHost(election);

        await host.TickAsync(new DateTime(2026, 7, 6), CancellationToken.None);

        Assert.Equal(TribeVotePhase.Candidacy, election.Phase);
    }

    [Fact]
    public async Task Armed_RepeatedTicksAcrossManyDays_StaysRegistrationOpenForever()
    {
        var election = CreateElection();
        var host = CreateHost(election);

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
        var host = CreateHost(election, testMode: true);

        await host.TickAsync(new DateTime(2026, 7, 6), CancellationToken.None);

        Assert.Equal(TribeVotePhase.Closed, election.Phase);
    }
}
