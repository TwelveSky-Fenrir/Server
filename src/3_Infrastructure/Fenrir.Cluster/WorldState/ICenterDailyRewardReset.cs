namespace Fenrir.Cluster.WorldState;

public interface ICenterDailyRewardReset
{
    public ValueTask ResetDailyRewardClaimsAsync(bool clearWeeklyDayCounter, CancellationToken ct);
}
