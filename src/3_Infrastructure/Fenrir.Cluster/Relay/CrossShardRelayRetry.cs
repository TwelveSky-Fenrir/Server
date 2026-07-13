namespace Fenrir.Cluster.Relay;

internal static class CrossShardRelayRetry
{
    private static readonly TimeSpan[] BackoffDelays =
        [TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(200)];

    public static async ValueTask RunAsync(Func<ValueTask> operation, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
            try
            {
                await operation().ConfigureAwait(false);
                return;
            }
            catch (Exception) when (attempt < BackoffDelays.Length && !ct.IsCancellationRequested)
            {
                await Task.Delay(BackoffDelays[attempt], ct).ConfigureAwait(false);
            }
    }

    public static async ValueTask RunSync(Action operation, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
            try
            {
                operation();
                return;
            }
            catch (Exception) when (attempt < BackoffDelays.Length && !ct.IsCancellationRequested)
            {
                await Task.Delay(BackoffDelays[attempt], ct).ConfigureAwait(false);
            }
    }
}
