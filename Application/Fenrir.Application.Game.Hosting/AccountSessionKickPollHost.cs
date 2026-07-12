using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Fenrir.Network.Serialization.Zone.Wire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class AccountSessionKickPollHost(
    SessionRegistry registry,
    ZoneRegistry zones,
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
            if (registry.TryGetByAccount(target.AccountId, out var session) &&
                session is ZoneClientSession { State: ZoneSessionState.InWorld, CharacterId: { } characterId } zoneSession &&
                zones.TryGetPlayer(characterId, out var runtimeState) &&
                !runtimeState.IsMovingZone)
            {
                logger.LogInformation(
                    "Kicking account {AccountId} from shard {ShardId}: a newer login claimed this account elsewhere",
                    target.AccountId, shardId);

                var notice = new AvatarStatUpdateResponse { Sort = LoginFromAnotherSort, Value = 0, Value2 = 0 };

                // Server/ts25zone/S01_MainApplication.cpp:125-126 sends this notice twice back-to-back before
                // closing the socket (B_AVATAR_CHANGE_INFO_2 already USENDs it, then the caller USENDs it again) --
                // mirrored here as two independent attempts rather than "cleaned up" to a single send.
                SendDuplicateLoginNotice(zoneSession, notice, target.AccountId, shardId);
                SendDuplicateLoginNotice(zoneSession, notice, target.AccountId, shardId);

                zoneSession.Abort(DisconnectReason.Evicted);
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

    private void SendDuplicateLoginNotice(
        ClientSession session, AvatarStatUpdateResponse notice, int accountId, int shardId)
    {
        try
        {
            session.Send(notice);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex,
                "Failed to send duplicate-login notice to account {AccountId} before evicting from shard {ShardId}",
                accountId, shardId);
        }
    }
}
