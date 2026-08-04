namespace Fenrir.Data.Abstractions.Characters;

public interface IDailyRewardResetRepository
{
    public ValueTask ApplyDailyRewardResetAsync(DateOnly occurrenceDate, bool clearWeeklyDayCounter,
        CancellationToken ct);

    public ValueTask<DailyRewardResetBroadcastClaimState> TryClaimDailyRewardResetBroadcastAsync(
        DateOnly occurrenceDate, byte shardId, Guid leaseId, int leaseDurationSeconds, CancellationToken ct);

    public ValueTask<bool> TryCompleteDailyRewardResetBroadcastAsync(DateOnly occurrenceDate, byte shardId,
        Guid leaseId, CancellationToken ct);
}
