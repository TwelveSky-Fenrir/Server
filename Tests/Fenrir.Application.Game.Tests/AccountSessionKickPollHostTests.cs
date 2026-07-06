using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Hosting;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests;

// Cross-process duplicate-login kick/refusal, Game-side poll host: a newer login claiming an account
// elsewhere must drop this shard's live Zone session for that account and clear the ownership row.
public class AccountSessionKickPollHostTests
{
    private const byte ShardId = 3;

    [Fact]
    public async Task PollOnceAsync_NoLocalSessions_SkipsTheRoundTripEntirely()
    {
        var registry = new SessionRegistry();
        var accountSessions = new FakeAccountSessionRepository();
        var host = CreateHost(registry, accountSessions);

        await host.PollOnceAsync(CancellationToken.None);

        Assert.Empty(accountSessions.RefreshCalls);
    }

    [Fact]
    public async Task PollOnceAsync_AccountFlaggedForKick_AbortsTheLiveSession_AndClearsTheOwnershipRow()
    {
        const int accountId = 42;
        var registry = new SessionRegistry();
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry.Register(session);
        registry.AssociateAccount(session.SessionId, accountId);

        var kickToken = Guid.NewGuid();
        var accountSessions = new FakeAccountSessionRepository
        {
            KickedAccounts = [new KickedAccountDto(accountId, kickToken)]
        };
        var host = CreateHost(registry, accountSessions);

        await host.PollOnceAsync(CancellationToken.None);

        Assert.Equal(DisconnectReason.Evicted, session.DisconnectReason);
        var refreshCall = Assert.Single(accountSessions.RefreshCalls);
        Assert.Equal(AccountSessionServerKind.Game, refreshCall.ServerKind);
        Assert.Equal(ShardId, refreshCall.ShardId);
        Assert.Contains(accountId, refreshCall.AccountIds);
        var cleared = Assert.Single(accountSessions.ClearedOwners);
        Assert.Equal((accountId, AccountSessionServerKind.Game, (byte?)ShardId, kickToken), cleared);
    }

    [Fact]
    public async Task PollOnceAsync_AccountFlaggedForKick_ButNoLongerHeldLocally_StillClearsTheRow_DoesNotThrow()
    {
        // The session already disconnected on its own between the DB flag being set and this poll running.
        const int accountId = 99;
        var registry = new SessionRegistry();
        var kickToken = Guid.NewGuid();
        var accountSessions = new FakeAccountSessionRepository
        {
            KickedAccounts = [new KickedAccountDto(accountId, kickToken)]
        };
        var host = CreateHost(registry, accountSessions);

        // Nothing locally registered for this account -- RefreshAndGetKickedAsync would never even be called in
        // production (SnapshotAssociatedAccountIds would be empty), but this proves the loop over the result set
        // itself tolerates a since-vanished local session without throwing.
        registry.Register(ZoneTestKit.CreateSession(2).Session);
        registry.AssociateAccount(2, 1); // an unrelated, still-live account so the snapshot isn't empty

        await host.PollOnceAsync(CancellationToken.None);

        Assert.Single(accountSessions.ClearedOwners, o => o.AccountId == accountId);
    }

    private static AccountSessionKickPollHost CreateHost(SessionRegistry registry,
        FakeAccountSessionRepository accountSessions)
    {
        return new AccountSessionKickPollHost(registry, accountSessions,
            Options.Create(new GameServerOptions { ShardId = ShardId }),
            NullLogger<AccountSessionKickPollHost>.Instance);
    }
}
