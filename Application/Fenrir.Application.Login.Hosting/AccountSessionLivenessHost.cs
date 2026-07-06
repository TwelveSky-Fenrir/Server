using Fenrir.Application.Login.Domain;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Hosting;

/// <summary>
///     Keeps every Login-held account's <c>runtime.AccountSessions.LastRefreshedUtc</c> warm so
///     <c>AccountSessionReapHost</c>'s 6-minute staleness sweep never reaps a session that is simply still sitting
///     at char-select/PIN-entry. Never kicks anything itself -- <see cref="IAccountSessionRepository.RefreshAndGetKickedAsync" />
///     always returns empty for <see cref="AccountSessionServerKind.Login" /> by construction (the kick-request flag
///     only ever gets set against a Game-side row).
/// </summary>
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
                // A missed refresh just risks a spurious reap next sweep -- never worth crashing the LoginServer.
                logger.LogError(ex, "Account session liveness refresh failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    public async ValueTask RefreshAsync(CancellationToken ct)
    {
        var accountIds = registry.SnapshotAssociatedAccountIds();
        if (accountIds.IsEmpty)
            return;

        var ids = accountIds.Select(id => (int)id).ToArray();

        // Return value is always empty for AccountSessionServerKind.Login by construction (see this type's
        // remarks) -- the call's only real job is the LastRefreshedUtc side effect.
        _ = await accountSessions.RefreshAndGetKickedAsync(AccountSessionServerKind.Login, null, ids, ct)
            .ConfigureAwait(false);
    }
}
