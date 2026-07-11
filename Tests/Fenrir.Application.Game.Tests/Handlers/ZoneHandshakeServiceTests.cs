using System.Text;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Compression;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers;

public class ZoneHandshakeServiceTests
{
    private const int AccountId = 7;
    private const int CharacterId = 501;
    private const byte ShardId = 1;
    private static readonly Guid SessionToken = Guid.NewGuid();

    [Fact]
    public async Task ConsumeTicketAsync_TicketValidAndSessionStillCurrent_ReturnsAcceptedWithSessionToken()
    {
        var tickets = new FakeSessionTicketRepository
        {
            TicketToReturn = new ConsumedTicketDto(CharacterId, ShardId, SessionToken)
        };
        var accountSessions = new FakeAccountSessionRepository { TransitionResult = true };
        var service = CreateService(tickets, accountSessions);
        var (session, _) = ZoneTestKit.CreateSession(1);

        var result = await service.ConsumeTicketAsync(EncodeObfuscatedAccountId(AccountId), 0, session,
            CancellationToken.None);

        Assert.Equal(ZoneHandshakeOutcome.Accepted, result.Outcome);
        Assert.Equal(AccountId, result.AccountId);
        Assert.Equal(CharacterId, result.CharacterId);
        Assert.Equal(SessionToken, result.SessionToken);
        Assert.Equal((short)0, result.AccountGrade);
        Assert.Equal((AccountId, SessionToken, ShardId), accountSessions.LastTransition);
    }

    [Fact]
    public async Task ConsumeTicketAsync_TicketCarriesGmGrade_PropagatesItToTheResult()
    {
        var tickets = new FakeSessionTicketRepository
        {
            TicketToReturn = new ConsumedTicketDto(CharacterId, ShardId, SessionToken, 1)
        };
        var accountSessions = new FakeAccountSessionRepository { TransitionResult = true };
        var service = CreateService(tickets, accountSessions);
        var (session, _) = ZoneTestKit.CreateSession(1);

        var result = await service.ConsumeTicketAsync(EncodeObfuscatedAccountId(AccountId), 0, session,
            CancellationToken.None);

        Assert.Equal((short)1, result.AccountGrade);
    }

    [Fact]
    public async Task ConsumeTicketAsync_ANewerLoginAlreadyClaimedTheAccount_ReturnsSessionSuperseded()
    {
        var tickets = new FakeSessionTicketRepository
        {
            TicketToReturn = new ConsumedTicketDto(CharacterId, ShardId, SessionToken)
        };
        var accountSessions = new FakeAccountSessionRepository { TransitionResult = false };
        var service = CreateService(tickets, accountSessions);
        var (session, _) = ZoneTestKit.CreateSession(1);

        var result = await service.ConsumeTicketAsync(EncodeObfuscatedAccountId(AccountId), 0, session,
            CancellationToken.None);

        Assert.Equal(ZoneHandshakeOutcome.SessionSuperseded, result.Outcome);
    }

    [Fact]
    public async Task ConsumeTicketAsync_NoTicketFound_ReturnsRejected_AndNeverChecksAccountSession()
    {
        var tickets = new FakeSessionTicketRepository { TicketToReturn = null };
        var accountSessions = new FakeAccountSessionRepository();
        var service = CreateService(tickets, accountSessions);
        var (session, _) = ZoneTestKit.CreateSession(1);

        var result = await service.ConsumeTicketAsync(EncodeObfuscatedAccountId(AccountId), 0, session,
            CancellationToken.None);

        Assert.Equal(ZoneHandshakeOutcome.Rejected, result.Outcome);
        Assert.Null(accountSessions.LastTransition);
    }

    [Fact]
    public async Task
        ConsumeTicketAsync_DecodedAccountIndexNotPositive_ReturnsProtocolViolation_AndNeverConsumesTheTicket()
    {
        var tickets = new FakeSessionTicketRepository
        {
            TicketToReturn = new ConsumedTicketDto(CharacterId, ShardId, SessionToken)
        };
        var accountSessions = new FakeAccountSessionRepository();
        var service = CreateService(tickets, accountSessions);
        var (session, _) = ZoneTestKit.CreateSession(1);

        var result = await service.ConsumeTicketAsync(EncodeObfuscatedAccountId(0), 0, session,
            CancellationToken.None);

        Assert.Equal(ZoneHandshakeOutcome.ProtocolViolation, result.Outcome);
        Assert.Null(accountSessions.LastTransition);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public async Task ConsumeTicketAsync_ThreeWayGroup_DeclaredTribeOutOfRange_ReturnsProtocolViolation(
        int declaredTribe)
    {
        var tickets = new FakeSessionTicketRepository
        {
            TicketToReturn = new ConsumedTicketDto(CharacterId, ShardId, SessionToken)
        };
        var accountSessions = new FakeAccountSessionRepository();
        var service = CreateService(tickets, accountSessions, TribeQuotaGroup.ThreeWay);
        var (session, _) = ZoneTestKit.CreateSession(1);

        var result = await service.ConsumeTicketAsync(EncodeObfuscatedAccountId(AccountId), declaredTribe, session,
            CancellationToken.None);

        Assert.Equal(ZoneHandshakeOutcome.ProtocolViolation, result.Outcome);
        Assert.Null(accountSessions.LastTransition);
    }

    [Fact]
    public async Task ConsumeTicketAsync_ThreeWayGroup_QuotaAlreadyFullForDeclaredTribe_ReturnsQuotaFull()
    {
        var tickets = new FakeSessionTicketRepository
        {
            TicketToReturn = new ConsumedTicketDto(CharacterId, ShardId, SessionToken)
        };
        var accountSessions = new FakeAccountSessionRepository();
        var quota = new TribeQuotaRegistry();

        for (var i = 0; i < 3; i++)
        {
            var (other, _) = ZoneTestKit.CreateSession(100 + i);
            quota.Record(other, 1, 900 + i, 9000 + i, DateTimeOffset.UtcNow);
        }

        var service = CreateService(tickets, accountSessions, TribeQuotaGroup.ThreeWay, quota, 9);
        var (session, _) = ZoneTestKit.CreateSession(1);

        var result = await service.ConsumeTicketAsync(EncodeObfuscatedAccountId(AccountId), 1, session,
            CancellationToken.None);

        Assert.Equal(ZoneHandshakeOutcome.QuotaFull, result.Outcome);
        Assert.Null(accountSessions.LastTransition);
    }

    [Fact]
    public async Task ConsumeTicketAsync_ThreeWayGroup_QuotaNotYetFull_ProceedsAndRecordsUpstreamResolvedTribe()
    {
        var tickets = new FakeSessionTicketRepository
        {
            TicketToReturn = new ConsumedTicketDto(CharacterId, ShardId, SessionToken)
        };
        var accountSessions = new FakeAccountSessionRepository { TransitionResult = true };
        var quota = new TribeQuotaRegistry();
        var characters = new FakeCharacterRepository
        {
            WorldEntryToReturn = new CharacterWorldEntryDto(CharacterId, AccountId, 0, "Hero", 2, 0, 0, 0,
                1, 1, 0, 0, 0, 0, 100, 100, 50, 50, 1)
        };

        var service = CreateService(tickets, accountSessions, TribeQuotaGroup.ThreeWay, quota, 300,
            characters);
        var (session, _) = ZoneTestKit.CreateSession(1);

        var result = await service.ConsumeTicketAsync(EncodeObfuscatedAccountId(AccountId), 1,
            session, CancellationToken.None);

        Assert.Equal(ZoneHandshakeOutcome.Accepted, result.Outcome);
        Assert.Equal(1, quota.CountForTribe(2));
        Assert.Equal(0, quota.CountForTribe(1));
    }

    [Fact]
    public async Task ConsumeTicketAsync_CharacterLookupReturnsNull_FallsBackToDeclaredTribeDefensively()
    {
        var tickets = new FakeSessionTicketRepository
        {
            TicketToReturn = new ConsumedTicketDto(CharacterId, ShardId, SessionToken)
        };
        var accountSessions = new FakeAccountSessionRepository { TransitionResult = true };
        var quota = new TribeQuotaRegistry();
        var characters = new FakeCharacterRepository { WorldEntryToReturn = null };

        var service = CreateService(tickets, accountSessions, TribeQuotaGroup.None, quota, 300,
            characters);
        var (session, _) = ZoneTestKit.CreateSession(1);

        var result = await service.ConsumeTicketAsync(EncodeObfuscatedAccountId(AccountId), 3,
            session, CancellationToken.None);

        Assert.Equal(ZoneHandshakeOutcome.Accepted, result.Outcome);
        Assert.Equal(1, quota.CountForTribe(3));
    }

    private static ZoneHandshakeService CreateService(FakeSessionTicketRepository tickets,
        FakeAccountSessionRepository accountSessions, TribeQuotaGroup quotaGroup = TribeQuotaGroup.None,
        TribeQuotaRegistry? quota = null, int capacity = 300, FakeCharacterRepository? characters = null)
    {
        return new ZoneHandshakeService(tickets, accountSessions, characters ?? new FakeCharacterRepository(),
            new FakeEventLogRepository(), quota ?? new TribeQuotaRegistry(),
            Options.Create(new GameServerOptions
                { ShardId = ShardId, TribeQuotaGroup = quotaGroup, Capacity = capacity }));
    }

        private static string EncodeObfuscatedAccountId(int accountId)
    {
        var bytes = Encoding.Latin1.GetBytes("MG" + accountId);
        WireXor.ApplyUidXor(bytes);
        return Encoding.Latin1.GetString(bytes);
    }
}
