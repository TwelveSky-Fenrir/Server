using System.Text;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Compression;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers;

// op11 ZC_TEMP_REGISTER_SEND -- cross-process duplicate-login kick/refusal, Game-side half: a resolved,
// shard-matched ticket must still be re-checked against runtime.AccountSessions before world-entry is granted,
// since the account may have logged in again elsewhere between ticket mint and ticket consume.
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

        var result = await service.ConsumeTicketAsync(EncodeObfuscatedAccountId(AccountId), CancellationToken.None);

        Assert.Equal(ZoneHandshakeOutcome.Accepted, result.Outcome);
        Assert.Equal(AccountId, result.AccountId);
        Assert.Equal(CharacterId, result.CharacterId);
        Assert.Equal(SessionToken, result.SessionToken);
        Assert.Equal((AccountId, SessionToken, ShardId), accountSessions.LastTransition);
    }

    [Fact]
    public async Task ConsumeTicketAsync_ANewerLoginAlreadyClaimedTheAccount_ReturnsSessionSuperseded()
    {
        // runtime.AccountSessions moved on since this ticket was minted (e.g. the account logged in again
        // elsewhere) -- usp_AccountSession_TransitionToGame refuses the claim even though the ticket itself
        // was perfectly valid.
        var tickets = new FakeSessionTicketRepository
        {
            TicketToReturn = new ConsumedTicketDto(CharacterId, ShardId, SessionToken)
        };
        var accountSessions = new FakeAccountSessionRepository { TransitionResult = false };
        var service = CreateService(tickets, accountSessions);

        var result = await service.ConsumeTicketAsync(EncodeObfuscatedAccountId(AccountId), CancellationToken.None);

        Assert.Equal(ZoneHandshakeOutcome.SessionSuperseded, result.Outcome);
    }

    [Fact]
    public async Task ConsumeTicketAsync_NoTicketFound_ReturnsRejected_AndNeverChecksAccountSession()
    {
        var tickets = new FakeSessionTicketRepository { TicketToReturn = null };
        var accountSessions = new FakeAccountSessionRepository();
        var service = CreateService(tickets, accountSessions);

        var result = await service.ConsumeTicketAsync(EncodeObfuscatedAccountId(AccountId), CancellationToken.None);

        Assert.Equal(ZoneHandshakeOutcome.Rejected, result.Outcome);
        Assert.Null(accountSessions.LastTransition);
    }

    private static ZoneHandshakeService CreateService(FakeSessionTicketRepository tickets,
        FakeAccountSessionRepository accountSessions)
    {
        return new ZoneHandshakeService(tickets, accountSessions,
            Options.Create(new GameServerOptions { ShardId = ShardId }));
    }

    /// <summary>Mirrors ObfuscatedUidCodec.TryDecodeAccountId's encoding half: Latin1("MG"+id), then USE_XOR_UID.</summary>
    private static string EncodeObfuscatedAccountId(int accountId)
    {
        var bytes = Encoding.Latin1.GetBytes("MG" + accountId);
        WireXor.ApplyUidXor(bytes);
        return Encoding.Latin1.GetString(bytes);
    }
}
