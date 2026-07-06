using System.Collections.Immutable;
using Fenrir.Application.Login.Hosting;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests;

// Cross-process duplicate-login kick/refusal, single unsharded backstop: reaps runtime.AccountSessions rows any
// process (Login or Game) abandoned without running its own teardown path.
public class AccountSessionReapHostTests
{
    [Fact]
    public async Task ReapOnceAsync_NothingStale_DoesNotThrow()
    {
        var accountSessions = new FakeAccountSessionRepository { ReapResult = ImmutableArray<ReapedAccountSessionDto>.Empty };
        var host = new AccountSessionReapHost(accountSessions, NullLogger<AccountSessionReapHost>.Instance);

        await host.ReapOnceAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ReapOnceAsync_StaleRowsReturned_CompletesWithoutThrowing_ForBothServerKinds()
    {
        var accountSessions = new FakeAccountSessionRepository
        {
            ReapResult =
            [
                new ReapedAccountSessionDto(1, (byte)AccountSessionServerKind.Login),
                new ReapedAccountSessionDto(2, (byte)AccountSessionServerKind.Game)
            ]
        };
        var host = new AccountSessionReapHost(accountSessions, NullLogger<AccountSessionReapHost>.Instance);

        await host.ReapOnceAsync(CancellationToken.None);
    }
}
