namespace Fenrir.Data.Abstractions.Characters;

public interface IDailyRewardResetRepository
{
    public ValueTask ResetDailyRewardClaimsAsync(bool clearWeeklyDayCounter, CancellationToken ct);
}
