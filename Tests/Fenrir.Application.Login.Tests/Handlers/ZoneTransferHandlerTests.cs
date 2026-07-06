using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Services.ZoneTransfer;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Network.Serialization.Wire;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Tests.Handlers;

// op22 CL_DEMAND_ZONE_SERVER_INFO_SEND -- previously audited bug (ADR-0012 point 4): FirstOrDefault() over the
// shard directory ignored which shard actually hosts the character's MapId, silently mis-routing instead of
// failing explicitly. Fixed in ZoneTransferService.ResolveShardForMapAsync; see the two "no shard hosts the
// map" tests below for the current, explicit-failure behavior.
public class ClDemandZoneServerInfoSendHandlerTests
{
    private const short HostedMapId = 42;

    private static readonly CharacterSummaryDto Summary = new(
        501, 0, "Hero", 1, 0, 1, 1, 10);

    private static readonly CharacterWorldEntryDto WorldEntry = new(
        501, 1, 0, "Hero", 1, 0, 1, 1,
        10, HostedMapId, 0, 0, 0, 0, 100, 100,
        50, 50, 0);

    private static readonly ShardDirectoryEntryDto Shard1 =
        new(1, "10.0.0.1", 30000, 0, 100, 0f);

    private static readonly ShardDirectoryEntryDto Shard2 =
        new(2, "10.0.0.2", 30001, 0, 100, 0f);

    [Fact]
    public async Task HandleAsync_MapHostedByNonFirstShard_RoutesToTheShardThatHostsIt()
    {
        var directory = new FakeGameServerDirectoryRepository(Shard1, Shard2);
        var shardMaps = new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>
        {
            [1] = [1],
            [2] = [HostedMapId]
        });
        var tickets = new FakeSessionTicketRepository();
        var handler = CreateHandler(directory, shardMaps, tickets);
        var (session, pipe) = CreateSessionInCharSelect(out var sessionToken);

        await handler.HandleAsync(new ZoneTransferRequest { AvatarPost = 0 }, session, CancellationToken.None);

        Assert.Equal((1, 501, (byte)2, 15, sessionToken, (short)0),
            tickets.LastCreatedTicket);
        Assert.Equal(LoginSessionState.HandoverIssued, session.State);
        await PacketAssert.AssertSentAsync(pipe, new ZoneTransferResponse
        {
            Result = 0, Ip = Shard2.Host, Port = Shard2.Port, Zone = HostedMapId
        });
    }

    // GM-BLOCK precondition: the Login-side account-grade fact (legacy uUserSort) must ride the same
    // handover ticket as the character/shard, never re-queried by the Zone session.
    [Fact]
    public async Task HandleAsync_GmAccount_CarriesAccountGradeIntoTheMintedTicket()
    {
        var directory = new FakeGameServerDirectoryRepository(Shard1, Shard2);
        var shardMaps = new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>
        {
            [1] = [1],
            [2] = [HostedMapId]
        });
        var tickets = new FakeSessionTicketRepository();
        var handler = CreateHandler(directory, shardMaps, tickets);
        var (session, _) = CreateSessionInCharSelect(out var sessionToken, 1);

        await handler.HandleAsync(new ZoneTransferRequest { AvatarPost = 0 }, session, CancellationToken.None);

        Assert.Equal((1, 501, (byte)2, 15, sessionToken, (short)1),
            tickets.LastCreatedTicket);
    }

    [Fact]
    public async Task HandleAsync_NoShardHostsTheMap_RespondsWithGenericFailureAndMintsNoTicket()
    {
        var directory = new FakeGameServerDirectoryRepository(Shard1, Shard2);
        var shardMaps = new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>
        {
            [1] = [1],
            [2] = [2]
        });
        var tickets = new FakeSessionTicketRepository();
        var handler = CreateHandler(directory, shardMaps, tickets);
        var (session, pipe) = CreateSessionInCharSelect(out _);

        await handler.HandleAsync(new ZoneTransferRequest { AvatarPost = 0 }, session, CancellationToken.None);

        Assert.Null(tickets.LastCreatedTicket);
        Assert.Equal(LoginSessionState.CharSelect, session.State);
        await PacketAssert.AssertSentAsync(pipe, new ZoneTransferResponse
        {
            Result = 1, Ip = "", Port = 0, Zone = 0
        });
    }

    [Fact]
    public async Task HandleAsync_NoLiveShardsAtAll_RespondsWithGenericFailureAndMintsNoTicket()
    {
        var directory = new FakeGameServerDirectoryRepository();
        var shardMaps = new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>());
        var tickets = new FakeSessionTicketRepository();
        var handler = CreateHandler(directory, shardMaps, tickets);
        var (session, pipe) = CreateSessionInCharSelect(out _);

        await handler.HandleAsync(new ZoneTransferRequest { AvatarPost = 0 }, session, CancellationToken.None);

        Assert.Null(tickets.LastCreatedTicket);
        Assert.Equal(LoginSessionState.CharSelect, session.State);
        await PacketAssert.AssertSentAsync(pipe, new ZoneTransferResponse
        {
            Result = 1, Ip = "", Port = 0, Zone = 0
        });
    }

    private static ZoneTransferHandler CreateHandler(FakeGameServerDirectoryRepository directory,
        FakeShardMapAssignmentRepository shardMaps, FakeSessionTicketRepository tickets)
    {
        var characters = FakeCharacterRepository.With(Summary, WorldEntry);
        var options = Options.Create(new LoginServerOptions());
        return new ZoneTransferHandler(new ZoneTransferService(characters, directory, shardMaps, tickets, options,
            NullLogger<ZoneTransferService>.Instance));
    }

    private static (LoginClientSession Session, FakeDuplexPipe Pipe) CreateSessionInCharSelect(out Guid sessionToken,
        short accountGrade = 0)
    {
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);
        session.MarkAuthenticated(1, accountGrade);
        sessionToken = Guid.NewGuid();
        session.MarkAccountSessionToken(sessionToken);
        session.MarkCharSelect();
        return (session, pipe);
    }
}
