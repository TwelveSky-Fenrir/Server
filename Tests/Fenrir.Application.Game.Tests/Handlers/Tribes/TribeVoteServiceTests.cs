using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Handlers.Handlers.Tribes;
using Fenrir.Application.Game.Services.Tribes;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

public class TribeVoteServiceTests
{
    private const int CharacterId = 42;

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State, TribeVoteElection Election
        , FakeWorldStateRepository Repository) Setup(
            short level = 145, int contributionPoints = 1000, byte tribe = 1)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, CharacterId);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(CharacterId,
            ZoneTestKit.EnterData(session, zone.MapId, tribe: tribe, level: level)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;

        zone.TryGetPlayer(CharacterId, out var state);
        state!.ContributionPoints = contributionPoints;
        // 145 (default level) + 12 (Level2) + 6 (RebirthCount) = 163, exactly
        // TribeVoteElection.MinimumEligibilityLevel -- keeps every pre-existing call site that doesn't care
        // about the eligibility gate itself passing without having to touch each one individually (mirrors
        // TribeVoteElectionTests.CreatePlayer's own default shape).
        state.Level2 = 12;
        state.RebirthCount = 6;

        var repository = new FakeWorldStateRepository();
        var worldState = new WorldStateService(repository, NullLogger<WorldStateService>.Instance);
        worldState.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options), new MovementRules(Options.Create(options)),
            new DirtyTracker<int>(), NullLogger<Zone>.Instance, ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize([1]);

        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), registry,
            NullLogger<TribeVoteElection>.Instance);

        return (session, pipe, state, election, repository);
    }

    [Theory]
    [InlineData(1, -1)]
    [InlineData(1, 10)]
    [InlineData(3, 999)]
    public async Task OutOfBoundsSlot_Aborts(int sort, int value)
    {
        // Sort/slot-range validation lives on the handler itself, ahead of the service call -- exercise the
        // real handler here, which the service's own (Sort-less) API can't express at all.
        var (session, _, _, election, _) = Setup();
        var handler = new TribeVoteHandler(new TribeVoteService(election, NullLogger<TribeVoteService>.Instance));

        await handler.HandleAsync(new TribeVoteRequest { Sort = sort, Value = value }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task UnsupportedSort_Aborts(int sort)
    {
        var (session, _, _, election, _) = Setup();
        var handler = new TribeVoteHandler(new TribeVoteService(election, NullLogger<TribeVoteService>.Instance));

        await handler.HandleAsync(new TribeVoteRequest { Sort = sort, Value = 0 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task Candidacy_WithNoElectionWindowOpen_Aborts()
    {
        var (_, _, state, election, _) = Setup();
        var service = new TribeVoteService(election, NullLogger<TribeVoteService>.Instance);

        var result = await service.RegisterCandidacyAsync(state, 0, CancellationToken.None);

        Assert.Equal(TribeVoteAction.Abort, result.Action);
    }

    [Fact]
    public async Task Candidacy_DuringCandidacyWindow_Succeeds_AndEchoesResult()
    {
        var (_, _, state, election, repository) = Setup();
        await election.OpenCandidacyWindowAsync(CancellationToken.None);
        var service = new TribeVoteService(election, NullLogger<TribeVoteService>.Instance);

        var result = await service.RegisterCandidacyAsync(state, 3, CancellationToken.None);

        Assert.Equal(TribeVoteAction.Accept, result.Action);
        Assert.Equal(0, result.Result);

        var votes = await repository.GetTribeVotesAsync(1, CancellationToken.None);
        Assert.Single(votes, v => v.SlotIndex == 3 && v.CandidateCharacterId == CharacterId);
    }

    [Fact]
    public async Task Candidacy_LevelTooLow_Aborts()
    {
        var (_, _, state, election, _) = Setup(100);
        await election.OpenCandidacyWindowAsync(CancellationToken.None);
        var service = new TribeVoteService(election, NullLogger<TribeVoteService>.Instance);

        var result = await service.RegisterCandidacyAsync(state, 0, CancellationToken.None);

        Assert.Equal(TribeVoteAction.Abort, result.Action);
    }

    [Fact]
    public async Task Vote_WithNoElectionWindowOpen_Aborts()
    {
        var (_, _, state, election, _) = Setup();
        var service = new TribeVoteService(election, NullLogger<TribeVoteService>.Instance);

        var result = await service.CastVoteAsync(state, 0, CancellationToken.None);

        Assert.Equal(TribeVoteAction.Abort, result.Action);
    }

    [Fact]
    public async Task Vote_ForAnOccupiedSlot_Succeeds()
    {
        var (_, _, state, election, repository) = Setup(tribe: 2);
        repository.VotesByTribe[2] = [new TribeVoteDto(2, 5, 999, 150, 1200, 0, DateTime.UtcNow)];
        election.OpenVotingWindow();
        var service = new TribeVoteService(election, NullLogger<TribeVoteService>.Instance);

        var result = await service.CastVoteAsync(state, 5, CancellationToken.None);

        Assert.Equal(TribeVoteAction.Accept, result.Action);
        Assert.Equal(0, result.Result);
    }

    [Fact]
    public async Task Vote_ForAnEmptySlot_Aborts()
    {
        var (_, _, state, election, _) = Setup(tribe: 2);
        election.OpenVotingWindow();
        var service = new TribeVoteService(election, NullLogger<TribeVoteService>.Instance);

        var result = await service.CastVoteAsync(state, 5, CancellationToken.None);

        Assert.Equal(TribeVoteAction.Abort, result.Action);
    }

    [Fact]
    public async Task Vote_ASecondTimeInTheSameWindow_RepliesFailure_InsteadOfAborting()
    {
        var (_, _, state, election, repository) = Setup(tribe: 2);
        repository.VotesByTribe[2] = [new TribeVoteDto(2, 5, 999, 150, 1200, 0, DateTime.UtcNow)];
        election.OpenVotingWindow();
        var service = new TribeVoteService(election, NullLogger<TribeVoteService>.Instance);
        await service.CastVoteAsync(state, 5, CancellationToken.None);

        var result = await service.CastVoteAsync(state, 5, CancellationToken.None);

        Assert.Equal(TribeVoteAction.RejectNoAbort, result.Action);
        Assert.Equal(1, result.Result);
    }
}
