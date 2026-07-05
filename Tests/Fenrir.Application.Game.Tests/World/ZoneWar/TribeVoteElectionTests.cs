using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.Tests.Handlers.Tribes;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.WorldState;
using Fenrir.Application.Game.World.ZoneWar;
using Fenrir.Data.Abstractions.Tribes;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeVoteElectionTests
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

    private static WorldStateService CreateWorldState(out FakeWorldStateRepository repository)
    {
        repository = new FakeWorldStateRepository();
        var service = new WorldStateService(repository, NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    private static PlayerRuntimeState CreatePlayer(int characterId, byte tribe, short level = 145,
        int contributionPoints = 1000, int rebirthCount = 0)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        return new PlayerRuntimeState
        {
            CharacterId = characterId,
            Session = session,
            Name = $"Hero{characterId}",
            Tribe = tribe,
            Gender = 0,
            HeadType = 0,
            FaceType = 0,
            Level = level,
            ContributionPoints = contributionPoints,
            RebirthCount = rebirthCount
        };
    }

    [Fact]
    public void Phase_StartsClosed()
    {
        var worldState = CreateWorldState(out _);
        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(),
            NullLogger<TribeVoteElection>.Instance);

        Assert.Equal(TribeVotePhase.Closed, election.Phase);
    }

    [Fact]
    public async Task OpenCandidacyWindowAsync_ClearsEveryTribesVotesAndOpensCandidacy()
    {
        var worldState = CreateWorldState(out var repository);
        repository.VotesByTribe[1] = [new TribeVoteDto(1, 0, 500, 200, 1200, 3, DateTime.UtcNow)];
        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(),
            NullLogger<TribeVoteElection>.Instance);

        await election.OpenCandidacyWindowAsync(CancellationToken.None);

        Assert.Equal(TribeVotePhase.Candidacy, election.Phase);
        Assert.Equal([(byte)0, (byte)1, (byte)2, (byte)3], repository.ClearCalls);
        Assert.Empty(await worldState.GetTribeVotesAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task TryRegisterCandidacyAsync_WhenWindowClosed_Rejects()
    {
        var worldState = CreateWorldState(out _);
        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(),
            NullLogger<TribeVoteElection>.Instance);
        var player = CreatePlayer(500, tribe: 1);

        var outcome = await election.TryRegisterCandidacyAsync(player, 0, CancellationToken.None);

        Assert.Equal(TribeVoteCandidacyOutcome.WindowClosed, outcome);
    }

    [Fact]
    public async Task TryRegisterCandidacyAsync_LevelBelowMinimum_Rejects()
    {
        var worldState = CreateWorldState(out _);
        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(),
            NullLogger<TribeVoteElection>.Instance);
        await election.OpenCandidacyWindowAsync(CancellationToken.None);
        var player = CreatePlayer(500, tribe: 1, level: 144);

        var outcome = await election.TryRegisterCandidacyAsync(player, 0, CancellationToken.None);

        Assert.Equal(TribeVoteCandidacyOutcome.LevelTooLow, outcome);
    }

    [Fact]
    public async Task TryRegisterCandidacyAsync_NotEnoughContribution_Rejects()
    {
        var worldState = CreateWorldState(out _);
        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(),
            NullLogger<TribeVoteElection>.Instance);
        await election.OpenCandidacyWindowAsync(CancellationToken.None);
        var player = CreatePlayer(500, tribe: 1, contributionPoints: 999);

        var outcome = await election.TryRegisterCandidacyAsync(player, 0, CancellationToken.None);

        Assert.Equal(TribeVoteCandidacyOutcome.NotEnoughContribution, outcome);
    }

    [Fact]
    public async Task TryRegisterCandidacyAsync_AlreadyHoldingAnotherSlot_Rejects()
    {
        var worldState = CreateWorldState(out var repository);
        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(),
            NullLogger<TribeVoteElection>.Instance);
        await election.OpenCandidacyWindowAsync(CancellationToken.None);
        repository.VotesByTribe[1] = [new TribeVoteDto(1, 3, 500, 145, 1000, 0, DateTime.UtcNow)];
        var player = CreatePlayer(500, tribe: 1);

        var outcome = await election.TryRegisterCandidacyAsync(player, 0, CancellationToken.None);

        Assert.Equal(TribeVoteCandidacyOutcome.AlreadyRegisteredInAnotherSlot, outcome);
    }

    [Fact]
    public async Task TryRegisterCandidacyAsync_SlotHeldByAStrongerCandidate_Rejects()
    {
        var worldState = CreateWorldState(out var repository);
        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(),
            NullLogger<TribeVoteElection>.Instance);
        await election.OpenCandidacyWindowAsync(CancellationToken.None);
        repository.VotesByTribe[1] = [new TribeVoteDto(1, 0, 999, 150, 5000, 0, DateTime.UtcNow)];
        var player = CreatePlayer(500, tribe: 1, contributionPoints: 5000); // equal, not strictly greater

        var outcome = await election.TryRegisterCandidacyAsync(player, 0, CancellationToken.None);

        Assert.Equal(TribeVoteCandidacyOutcome.SlotHeldByStrongerCandidate, outcome);
    }

    [Fact]
    public async Task TryRegisterCandidacyAsync_DisplacesAWeakerCandidate_AndRegisters()
    {
        var worldState = CreateWorldState(out var repository);
        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(),
            NullLogger<TribeVoteElection>.Instance);
        await election.OpenCandidacyWindowAsync(CancellationToken.None);
        repository.VotesByTribe[1] = [new TribeVoteDto(1, 0, 999, 150, 1200, 0, DateTime.UtcNow)];
        var player = CreatePlayer(500, tribe: 1, contributionPoints: 1201);

        var outcome = await election.TryRegisterCandidacyAsync(player, 0, CancellationToken.None);

        Assert.Equal(TribeVoteCandidacyOutcome.Registered, outcome);
        var votes = await worldState.GetTribeVotesAsync(1, CancellationToken.None);
        var slot0 = Assert.Single(votes);
        Assert.Equal(500, slot0.CandidateCharacterId);
        Assert.Equal(1201, slot0.KillOtherTribeCount);
    }

    [Fact]
    public async Task TryCastVoteAsync_WhenWindowClosed_Rejects()
    {
        var worldState = CreateWorldState(out _);
        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(),
            NullLogger<TribeVoteElection>.Instance);
        var player = CreatePlayer(500, tribe: 1);

        var outcome = await election.TryCastVoteAsync(player, 0, CancellationToken.None);

        Assert.Equal(TribeVoteCastOutcome.WindowClosed, outcome);
    }

    [Fact]
    public async Task TryCastVoteAsync_SlotEmpty_Rejects()
    {
        var worldState = CreateWorldState(out _);
        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(),
            NullLogger<TribeVoteElection>.Instance);
        await election.OpenCandidacyWindowAsync(CancellationToken.None);
        election.OpenVotingWindow();
        var player = CreatePlayer(500, tribe: 1);

        var outcome = await election.TryCastVoteAsync(player, 0, CancellationToken.None);

        Assert.Equal(TribeVoteCastOutcome.SlotEmpty, outcome);
    }

    [Fact]
    public async Task TryCastVoteAsync_Success_AddsComputedPoints()
    {
        var worldState = CreateWorldState(out var repository);
        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(),
            NullLogger<TribeVoteElection>.Instance);
        await election.OpenCandidacyWindowAsync(CancellationToken.None);
        repository.VotesByTribe[1] = [new TribeVoteDto(1, 0, 999, 150, 1200, 0, DateTime.UtcNow)];
        election.OpenVotingWindow();
        var voter = CreatePlayer(500, tribe: 1, level: 150, rebirthCount: 2);

        var outcome = await election.TryCastVoteAsync(voter, 0, CancellationToken.None);

        Assert.Equal(TribeVoteCastOutcome.Cast, outcome);
        var votes = await worldState.GetTribeVotesAsync(1, CancellationToken.None);
        // aLevel1 + (aLevel2 + aRebirthNum) * 3 - 112 => 150 + 2*3 - 112 = 44
        Assert.Equal(44, votes[0].VotePoint);
    }

    [Fact]
    public async Task TryCastVoteAsync_SecondVoteInSameWindow_RepliesAlreadyVoted_WithoutDoubleCounting()
    {
        var worldState = CreateWorldState(out var repository);
        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(),
            NullLogger<TribeVoteElection>.Instance);
        await election.OpenCandidacyWindowAsync(CancellationToken.None);
        repository.VotesByTribe[1] = [new TribeVoteDto(1, 0, 999, 150, 1200, 0, DateTime.UtcNow)];
        election.OpenVotingWindow();
        var voter = CreatePlayer(500, tribe: 1, level: 150);

        Assert.Equal(TribeVoteCastOutcome.Cast, await election.TryCastVoteAsync(voter, 0, CancellationToken.None));
        Assert.Equal(TribeVoteCastOutcome.AlreadyVotedThisWindow,
            await election.TryCastVoteAsync(voter, 0, CancellationToken.None));

        Assert.Single(repository.AddPointsCalls);
    }

    [Fact]
    public async Task TryCastVoteAsync_AfterNewVotingWindowReopens_CanVoteAgain()
    {
        var worldState = CreateWorldState(out var repository);
        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(),
            NullLogger<TribeVoteElection>.Instance);
        await election.OpenCandidacyWindowAsync(CancellationToken.None);
        repository.VotesByTribe[1] = [new TribeVoteDto(1, 0, 999, 150, 1200, 0, DateTime.UtcNow)];
        election.OpenVotingWindow();
        var voter = CreatePlayer(500, tribe: 1, level: 150);
        await election.TryCastVoteAsync(voter, 0, CancellationToken.None);

        election.OpenVotingWindow(); // a fresh cycle -- clears the per-window voted set

        var outcome = await election.TryCastVoteAsync(voter, 0, CancellationToken.None);

        Assert.Equal(TribeVoteCastOutcome.Cast, outcome);
        Assert.Equal(2, repository.AddPointsCalls.Count);
    }

    [Fact]
    public async Task TallyForceLeaderAsync_AppointsTheHighestVotePointCandidate_AndMirrorsRoleOntoOnlinePlayers()
    {
        var worldState = CreateWorldState(out var repository);
        repository.VotesByTribe[1] =
        [
            new TribeVoteDto(1, 0, 100, 150, 1000, 5, DateTime.UtcNow),
            new TribeVoteDto(1, 1, 200, 150, 1000, 9, DateTime.UtcNow)
        ];
        var tribeRepository = new FakeTribeRepository { Tribes = [new TribeSummaryDto(1, 100, null)] };

        var registry = CreateRegistry(1);
        var (oldLeaderSession, oldLeaderPipe) = ZoneTestKit.CreateSession(1);
        var (winnerSession, winnerPipe) = ZoneTestKit.CreateSession(2);
        registry[1].Post(ZoneCommand.Enter(100, ZoneTestKit.EnterData(oldLeaderSession, 1, tribe: 1)));
        registry[1].Post(ZoneCommand.Enter(200, ZoneTestKit.EnterData(winnerSession, 1, tribe: 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(oldLeaderPipe);
        ZoneTestKit.DrainOutbound(winnerPipe);
        registry[1].TryGetPlayer(100, out var oldLeaderBefore);
        oldLeaderBefore!.TribeRole = 1; // was Force Leader before this tally

        var election = new TribeVoteElection(worldState, tribeRepository, registry,
            NullLogger<TribeVoteElection>.Instance);

        var winnerId = await election.TallyForceLeaderAsync(1, CancellationToken.None);
        registry[1].Tick(TimeSpan.FromMilliseconds(50)); // drains the posted TribeProgressZoneCommand mirrors

        Assert.Equal(200, winnerId);
        Assert.Equal((byte)1, tribeRepository.SetMasterCalls[0].TribeId);
        Assert.Equal(200, tribeRepository.SetMasterCalls[0].NewMasterCharacterId);

        registry[1].TryGetPlayer(100, out var oldLeaderState);
        registry[1].TryGetPlayer(200, out var winnerState);
        Assert.Equal((byte)0, oldLeaderState!.TribeRole);
        Assert.Equal((byte)1, winnerState!.TribeRole);
    }

    [Fact]
    public async Task TallyForceLeaderAsync_NoCandidateReachedOneVotePoint_LeavesTribeLeaderless()
    {
        var worldState = CreateWorldState(out var repository);
        repository.VotesByTribe[2] = [new TribeVoteDto(2, 0, 300, 150, 1000, 0, DateTime.UtcNow)];
        var tribeRepository = new FakeTribeRepository { Tribes = [new TribeSummaryDto(2, 300, null)] };
        var election = new TribeVoteElection(worldState, tribeRepository, CreateRegistry(),
            NullLogger<TribeVoteElection>.Instance);

        var winnerId = await election.TallyForceLeaderAsync(2, CancellationToken.None);

        Assert.Null(winnerId);
        Assert.Null(tribeRepository.SetMasterCalls[0].NewMasterCharacterId);
    }

    [Fact]
    public void CloseWindow_RejectsBothCandidacyAndVoting()
    {
        var worldState = CreateWorldState(out _);
        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(),
            NullLogger<TribeVoteElection>.Instance);
        election.OpenVotingWindow();

        election.CloseWindow();

        Assert.Equal(TribeVotePhase.Closed, election.Phase);
    }

    [Fact]
    public async Task PhaseTransitions_WithinOneCycle_ShareTheSameCorrelationId_AcrossEveryLogScope()
    {
        var worldState = CreateWorldState(out _);
        var logger = new CapturingLogger<TribeVoteElection>();
        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(), logger);

        await election.OpenCandidacyWindowAsync(CancellationToken.None);
        election.OpenVotingWindow();
        election.CloseWindow();

        Assert.Equal(3, logger.Scopes.Count); // one scope per phase transition
        var cycleIds = logger.Scopes
            .Select(CapturingLogger<TribeVoteElection>.PropertiesOf)
            .Select(props => (Guid)props.Single(p => p.Key == "TribeVoteCycleId").Value)
            .Distinct()
            .ToList();
        Assert.Single(cycleIds);
        Assert.NotEqual(Guid.Empty, cycleIds[0]);
    }

    [Fact]
    public async Task OpenCandidacyWindowAsync_CalledForASecondCycle_MintsADifferentCorrelationId()
    {
        var worldState = CreateWorldState(out _);
        var logger = new CapturingLogger<TribeVoteElection>();
        var election = new TribeVoteElection(worldState, new FakeTribeRepository(), CreateRegistry(), logger);

        await election.OpenCandidacyWindowAsync(CancellationToken.None);
        await election.OpenCandidacyWindowAsync(CancellationToken.None);

        var cycleIds = logger.Scopes
            .Select(CapturingLogger<TribeVoteElection>.PropertiesOf)
            .Select(props => (Guid)props.Single(p => p.Key == "TribeVoteCycleId").Value)
            .ToList();
        Assert.Equal(2, cycleIds.Count);
        Assert.NotEqual(cycleIds[0], cycleIds[1]);
    }

    [Fact]
    public async Task TallyForceLeaderAsync_ScopesTheTribeIdAlongsideTheCycleId_AndLogsTheWinner()
    {
        var worldState = CreateWorldState(out var repository);
        var tribeRepository = new FakeTribeRepository { Tribes = [new TribeSummaryDto(1, 100, null)] };
        var logger = new CapturingLogger<TribeVoteElection>();
        var election = new TribeVoteElection(worldState, tribeRepository, CreateRegistry(), logger);
        await election.OpenCandidacyWindowAsync(CancellationToken.None); // mints the cycle id the tally scope reuses
        repository.VotesByTribe[1] = [new TribeVoteDto(1, 0, 555, 150, 1000, 7, DateTime.UtcNow)];

        var winnerId = await election.TallyForceLeaderAsync(1, CancellationToken.None);

        Assert.Equal(555, winnerId);
        var tallyScope = CapturingLogger<TribeVoteElection>.PropertiesOf(logger.Scopes[^1]);
        Assert.Equal((byte)1, tallyScope.Single(p => p.Key == "TribeId").Value);
        Assert.Contains(logger.Entries, e => e.Message.Contains("winner=555", StringComparison.Ordinal));
    }
}
