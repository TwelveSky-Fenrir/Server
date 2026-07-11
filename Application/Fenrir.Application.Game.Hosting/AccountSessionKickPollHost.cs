using Fenrir.Application.Game.Domain;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class AccountSessionKickPollHost(
    SessionRegistry registry,
    IAccountSessionRepository accountSessions,
    IOptions<GameServerOptions> options,
    ILogger<AccountSessionKickPollHost> logger) : BackgroundService
{
    private const int LoginFromAnotherSort = 903;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.AccountSessionPollIntervalSeconds));

        do
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Account session kick poll failed for shard {ShardId}", options.Value.ShardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    public async ValueTask PollOnceAsync(CancellationToken ct)
    {
        var accountIds = registry.SnapshotAssociatedAccountIds();
        if (accountIds.IsEmpty)
            return;

        var shardId = options.Value.ShardId;
        var ids = accountIds.Select(id => (int)id).ToArray();

        var kicked = await accountSessions.RefreshAndGetKickedAsync(AccountSessionServerKind.Game, shardId, ids, ct)
            .ConfigureAwait(false);

        foreach (var target in kicked)
        {
            if (registry.TryGetByAccount(target.AccountId, out var session))
            {
                logger.LogInformation(
                    "Kicking account {AccountId} from shard {ShardId}: a newer login claimed this account elsewhere",
                    target.AccountId, shardId);

                try
                {
                    session!.Send(new AvatarStatUpdateResponse { Sort = LoginFromAnotherSort, Value = 0, Value2 = 0 });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogDebug(ex,
                        "Failed to send duplicate-login notice to account {AccountId} before evicting from shard {ShardId}",
                        target.AccountId, shardId);
                }

                session!.Abort(DisconnectReason.Evicted);
            }

            try
            {
                await accountSessions
                    .ClearIfOwnerAsync(target.AccountId, AccountSessionServerKind.Game, shardId, target.SessionToken,
                        ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "Failed to clear runtime.AccountSessions row for kicked account {AccountId} on shard {ShardId}",
                    target.AccountId, shardId);
            }
        }
    }
}
