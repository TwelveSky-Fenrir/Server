using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Hosting;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Tests;

// Cross-process duplicate-login kick/refusal, Login-side liveness half: keeps runtime.AccountSessions'
// LastRefreshedUtc warm for every account this process holds a live Login session for.
public class AccountSessionLivenessHostTests
{
    [Fact]
    public async Task RefreshAsync_NoLocalSessions_SkipsTheRoundTripEntirely()
    {
        var registry = new SessionRegistry();
        var accountSessions = new FakeAccountSessionRepository();
        var host = CreateHost(registry, accountSessions);

        await host.RefreshAsync(CancellationToken.None);

        Assert.Empty(accountSessions.RefreshCalls);
    }

    [Fact]
    public async Task RefreshAsync_WithLocalSessions_RefreshesEveryAssociatedAccountId_AsLoginServerKind()
    {
        var registry = new SessionRegistry();
        registry.AssociateAccount(1, 100);
        registry.AssociateAccount(2, 200);
        var accountSessions = new FakeAccountSessionRepository();
        var host = CreateHost(registry, accountSessions);

        await host.RefreshAsync(CancellationToken.None);

        var call = Assert.Single(accountSessions.RefreshCalls);
        Assert.Equal(AccountSessionServerKind.Login, call.ServerKind);
        Assert.Null(call.ShardId);
        Assert.Equal([100, 200], call.AccountIds.OrderBy(id => id));
    }

    private static AccountSessionLivenessHost CreateHost(SessionRegistry registry,
        FakeAccountSessionRepository accountSessions)
    {
        return new AccountSessionLivenessHost(registry, accountSessions, Options.Create(new LoginServerOptions()),
            NullLogger<AccountSessionLivenessHost>.Instance);
    }
}
