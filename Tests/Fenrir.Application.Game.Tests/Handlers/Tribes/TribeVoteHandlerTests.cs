using System.Buffers.Binary;
using Fenrir.Application.Game.Handlers.Tribes;
using Fenrir.Application.Game.Handlers.Tribes.Services;
using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.WorldState;
using Fenrir.Application.Game.World.ZoneWar;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Tribes;
using Fenrir.Data.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Framing;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

public class TribeVoteHandlerTests
{
    private const int CharacterId = 42;
    private static int OneFrame => FrameWriter.FrameSizeOf<TribeVoteResponse>();

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State, TribeVoteElection Election, FakeWorldStateRepository Repository) Setup(
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
        var (session, _, _, election, _) = Setup();
        var handler = new TribeVoteHandler(new TribeVoteService(election));

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
        var handler = new TribeVoteHandler(new TribeVoteService(election));

        await handler.HandleAsync(new TribeVoteRequest { Sort = sort, Value = 0 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task Candidacy_WithNoElectionWindowOpen_Aborts()
    {
        var (session, _, _, election, _) = Setup();
        var handler = new TribeVoteHandler(new TribeVoteService(election));

        await handler.HandleAsync(new TribeVoteRequest { Sort = 1, Value = 0 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task Candidacy_DuringCandidacyWindow_Succeeds_AndEchoesSortAndValue()
    {
        var (session, pipe, _, election, repository) = Setup();
        await election.OpenCandidacyWindowAsync(CancellationToken.None);
        var handler = new TribeVoteHandler(new TribeVoteService(election));

        await handler.HandleAsync(new TribeVoteRequest { Sort = 1, Value = 3 }, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame, frame.Length);
        var payload = frame.AsSpan(1);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(payload[8..]));

        var votes = await repository.GetTribeVotesAsync(1, CancellationToken.None);
        Assert.Single(votes, v => v.SlotIndex == 3 && v.CandidateCharacterId == CharacterId);
    }

    [Fact]
    public async Task Candidacy_LevelTooLow_Aborts()
    {
        var (session, _, _, election, _) = Setup(level: 100);
        await election.OpenCandidacyWindowAsync(CancellationToken.None);
        var handler = new TribeVoteHandler(new TribeVoteService(election));

        await handler.HandleAsync(new TribeVoteRequest { Sort = 1, Value = 0 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task Vote_WithNoElectionWindowOpen_Aborts()
    {
        var (session, _, _, election, _) = Setup();
        var handler = new TribeVoteHandler(new TribeVoteService(election));

        await handler.HandleAsync(new TribeVoteRequest { Sort = 3, Value = 0 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task Vote_ForAnOccupiedSlot_Succeeds()
    {
        var (session, pipe, _, election, repository) = Setup(tribe: 2);
        repository.VotesByTribe[2] = [new TribeVoteDto(2, 5, 999, 150, 1200, 0, DateTime.UtcNow)];
        election.OpenVotingWindow();
        var handler = new TribeVoteHandler(new TribeVoteService(election));

        await handler.HandleAsync(new TribeVoteRequest { Sort = 3, Value = 5 }, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        var frame = ZoneTestKit.DrainOutbound(pipe);
        var payload = frame.AsSpan(1);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        Assert.Equal(5, BinaryPrimitives.ReadInt32LittleEndian(payload[8..]));
    }

    [Fact]
    public async Task Vote_ForAnEmptySlot_Aborts()
    {
        var (session, _, _, election, _) = Setup(tribe: 2);
        election.OpenVotingWindow();
        var handler = new TribeVoteHandler(new TribeVoteService(election));

        await handler.HandleAsync(new TribeVoteRequest { Sort = 3, Value = 5 }, session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task Vote_ASecondTimeInTheSameWindow_RepliesFailure_InsteadOfDisconnecting()
    {
        var (session, pipe, _, election, repository) = Setup(tribe: 2);
        repository.VotesByTribe[2] = [new TribeVoteDto(2, 5, 999, 150, 1200, 0, DateTime.UtcNow)];
        election.OpenVotingWindow();
        var handler = new TribeVoteHandler(new TribeVoteService(election));
        await handler.HandleAsync(new TribeVoteRequest { Sort = 3, Value = 5 }, session, CancellationToken.None);
        ZoneTestKit.DrainOutbound(pipe);

        await handler.HandleAsync(new TribeVoteRequest { Sort = 3, Value = 5 }, session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        var payload = ZoneTestKit.DrainOutbound(pipe).AsSpan(1);
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(payload));
    }
}
