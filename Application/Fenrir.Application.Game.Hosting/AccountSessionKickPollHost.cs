using Fenrir.Application.Game.Domain;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

/// <summary>
///     Cross-process duplicate-login kick/refusal, Game-side half: periodically checks whether any account this
///     shard holds a live Zone session for has been flagged for kick in <c>runtime.AccountSessions</c> (a newer
///     login claimed the account elsewhere) and, if so, drops that session. The refresh side effect of the same
///     call also keeps this shard's rows warm against <c>AccountSessionReapHost</c>'s 6-minute staleness sweep.
/// </summary>
/// <remarks>
///     No client-visible kick notification is sent (matches the Login-side duplicate-kick's own silent-drop
///     precedent, <c>LoginService.LoginAsync</c>'s <c>ConflictLogin</c> branch) -- a richer client-visible signal
///     (legacy's <c>B_AVATAR_CHANGE_INFO_2</c> sort=903 notification) is deliberately deferred: minting a new
///     <c>[FenrirPacket]</c> wire struct is outside this layer's scope and needs its real legacy opcode value
///     confirmed via <c>fenrir-wire-protocol-implementer</c>/<c>legacy-behavior-translator</c> first, not guessed
///     here.
/// </remarks>
public sealed class AccountSessionKickPollHost(
    SessionRegistry registry,
    IAccountSessionRepository accountSessions,
    IOptions<GameServerOptions> options,
    ILogger<AccountSessionKickPollHost> logger) : BackgroundService
{
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
                // A missed poll just delays a kick by one cycle -- never worth crashing the GameServer over.
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
