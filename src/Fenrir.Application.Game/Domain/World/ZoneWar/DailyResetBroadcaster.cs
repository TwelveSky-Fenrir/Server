using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class DailyResetBroadcaster(
    IDailyRewardResetRepository dailyRewardReset,
    IDailyResetZoneEventPublisher publisher,
    IOptions<GameServerOptions> serverOptions,
    ILogger<DailyResetBroadcaster> logger)
{
    private const int BroadcastLeaseDurationSeconds = 60;

    private readonly DailyResetBroadcastScheduler _scheduler = new();
    private readonly byte _shardId = serverOptions.Value.ShardId;

    public async ValueTask TickAsync(DateTimeOffset localNow, CancellationToken ct)
    {
        if (!_scheduler.IsDue(localNow))
            return;

        var occurrenceDate = DateOnly.FromDateTime(localNow.DateTime);
        var clearWeeklyDayCounter = localNow.DayOfWeek == DayOfWeek.Monday;
        try
        {
            await dailyRewardReset.ApplyDailyRewardResetAsync(occurrenceDate, clearWeeklyDayCounter, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "Daily reward-claim reset occurrence was not durably applied for {OccurrenceDate}", occurrenceDate);
            return;
        }

        var leaseId = Guid.NewGuid();
        DailyRewardResetBroadcastClaimState broadcastClaimState;

        try
        {
            broadcastClaimState = await dailyRewardReset.TryClaimDailyRewardResetBroadcastAsync(occurrenceDate,
                _shardId, leaseId, BroadcastLeaseDurationSeconds, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "Daily reward-claim reset broadcast occurrence could not be claimed for {OccurrenceDate}",
                occurrenceDate);
            return;
        }

        if (broadcastClaimState == DailyRewardResetBroadcastClaimState.Completed)
        {
            _scheduler.MarkCompleted(occurrenceDate);
            return;
        }

        if (broadcastClaimState != DailyRewardResetBroadcastClaimState.Claimed)
            return;

        try
        {
            await publisher.PublishAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Daily reward-claim reset broadcast failed for {OccurrenceDate}; its durable lease will be retried",
                occurrenceDate);
            return;
        }

        bool broadcastCompleted;

        try
        {
            broadcastCompleted = await dailyRewardReset.TryCompleteDailyRewardResetBroadcastAsync(occurrenceDate,
                _shardId, leaseId, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "Daily reward-claim reset broadcast completion could not be persisted for {OccurrenceDate}",
                occurrenceDate);
            return;
        }

        if (!broadcastCompleted)
        {
            logger.LogWarning(
                "Daily reward-claim reset broadcast lease was not current for {OccurrenceDate}; retry remains pending",
                occurrenceDate);
            return;
        }

        _scheduler.MarkCompleted(occurrenceDate);
    }
}
