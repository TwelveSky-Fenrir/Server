using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Hosting;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests;

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
    public async Task PollOnceAsync_AccountFlaggedForKick_SendsLoginFromAnotherNotice_BeforeAborting()
    {
        const int accountId = 42;
        var registry = new SessionRegistry();
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry.Register(session);
        registry.AssociateAccount(session.SessionId, accountId);

        var kickToken = Guid.NewGuid();
        var accountSessions = new FakeAccountSessionRepository
        {
            KickedAccounts = [new KickedAccountDto(accountId, kickToken)]
        };
        var host = CreateHost(registry, accountSessions);

        await host.PollOnceAsync(CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe, new AvatarStatUpdateResponse { Sort = 903, Value = 0, Value2 = 0 });
        Assert.Equal(DisconnectReason.Evicted, session.DisconnectReason);
    }

    [Fact]
    public async Task PollOnceAsync_AccountFlaggedForKick_ButNoLongerHeldLocally_StillClearsTheRow_DoesNotThrow()
    {
        const int accountId = 99;
        var registry = new SessionRegistry();
        var kickToken = Guid.NewGuid();
        var accountSessions = new FakeAccountSessionRepository
        {
            KickedAccounts = [new KickedAccountDto(accountId, kickToken)]
        };
        var host = CreateHost(registry, accountSessions);

        registry.Register(ZoneTestKit.CreateSession(2).Session);
        registry.AssociateAccount(2, 1);

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
