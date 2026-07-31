namespace Fenrir.Cluster.Center.WorldState;

public interface ICenterDailyRewardReset
{
    public ValueTask ResetDailyRewardClaimsAsync(bool clearWeeklyDayCounter, CancellationToken ct);
}
