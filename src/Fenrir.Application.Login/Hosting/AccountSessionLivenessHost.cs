using Fenrir.Application.Login.Sessions;
using Fenrir.Domain.Login;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Login;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Hosting;

public sealed class AccountSessionLivenessHost(
    SessionRegistry registry,
    IAccountSessionRepository accountSessions,
    IOptions<LoginServerOptions> options,
    ILogger<AccountSessionLivenessHost> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.AccountSessionRefreshIntervalSeconds));

        do
        {
            try
            {
                await RefreshAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Account session liveness refresh failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    public async ValueTask RefreshAsync(CancellationToken ct)
    {
        var accountIds = registry.SnapshotAssociatedAccountIds();
        if (accountIds.IsEmpty)
            return;

        var leases = new List<AccountSessionLeaseTvp>(accountIds.Length);
        var sessionsByAccount = new Dictionary<int, LoginClientSession>(accountIds.Length);

        foreach (var accountId in accountIds)
        {
            if (!registry.TryGetByAccount(accountId, out var session) || session is not LoginClientSession login)
                continue;

            if (login.State == LoginSessionState.HandoverIssued)
                continue;

            if (login.AccountSessionToken is not { } token)
                continue;

            var id = (int)accountId;
            leases.Add(new AccountSessionLeaseTvp(id, token));
            sessionsByAccount[id] = login;
        }

        if (leases.Count == 0)
            return;

        var held = await accountSessions
            .RefreshAndGetHeldLeasesAsync(AccountSessionServerKind.Login, null, leases, ct)
            .ConfigureAwait(false);

        var stillHeld = new HashSet<int>(held.Length);
        foreach (var row in held)
            stillHeld.Add(row.AccountId);

        foreach (var lease in leases)
        {
            if (stillHeld.Contains(lease.AccountId))
                continue;

            logger.LogWarning(
                "Account {AccountId}: runtime.AccountSessions lease lost (row gone or reclaimed by another " +
                "session token) -- closing session {SessionId}",
                lease.AccountId, sessionsByAccount[lease.AccountId].SessionId);

            sessionsByAccount[lease.AccountId].Abort(DisconnectReason.Evicted);
        }
    }
}
