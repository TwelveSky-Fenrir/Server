using Fenrir.Application.Game.Domain;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
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
///     Notify-then-disconnect, matching legacy's cross-process eviction handler: on a matched kick, legacy sends
///     the OLD session one <c>B_AVATAR_CHANGE_INFO_2</c> packet (<c>sort=S903LOGIN_FROM_ANOTHER=903</c>,
///     <c>value=0</c>) before tearing the connection down (Server/ts25zone/S01_MainApplication.cpp:110-129;
///     Server/ts25zone/S05_MyTransfer.cpp:519-542 for the wire shape; Server/Header/Protocol/STRUCT.h:1646-1651
///     for the named <c>S903LOGIN_FROM_ANOTHER</c> constant). <see cref="AvatarStatUpdateResponse" /> is Fenrir's
///     existing struct for this same wire opcode (<c>ZCP_AVATAR_CHANGE_INFO_2</c> = 22, already reused
///     elsewhere in this codebase for other <c>Sort</c>-coded emissions of this packet family, e.g.
///     <c>Zone.Combat.GrantWarPoints</c>) -- reused here rather than minting a second, colliding registration
///     for the same (server, direction, opcode) tuple. What the legacy client actually renders on receipt of
///     sort 903 is not sourced anywhere in this repository (client source is not present under <c>Server/</c>)
///     and remains an open item for client-side/protocol research; only the fact, content, and ordering of the
///     notify-then-disconnect are implemented here.
/// </remarks>
public sealed class AccountSessionKickPollHost(
    SessionRegistry registry,
    IAccountSessionRepository accountSessions,
    IOptions<GameServerOptions> options,
    ILogger<AccountSessionKickPollHost> logger) : BackgroundService
{
    /// <summary>
    ///     <c>S903LOGIN_FROM_ANOTHER</c> (Server/Header/Protocol/STRUCT.h:1646-1651), the <c>ECHANGE</c>-family sort
    ///     code legacy sends via <c>B_AVATAR_CHANGE_INFO_2</c> to a session being evicted by a newer login
    ///     elsewhere.
    /// </summary>
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

                try
                {
                    // Best-effort notice: the packet carries no ack and the eviction must proceed either way
                    // (matches the contract's own "successful case has no failure indicator" semantics), so a
                    // send failure here (e.g. the peer already tore its own socket down) must not block Abort.
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
