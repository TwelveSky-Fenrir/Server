using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch.FloodProtection;

public sealed class FirewallAllowlistReconcileService
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(120);

    private readonly ILogger<FirewallAllowlistReconcileService>? _logger;

    private readonly Func<CancellationToken, ValueTask> _reconcileAllowlistAsync;

    public FirewallAllowlistReconcileService(
        Func<CancellationToken, ValueTask> reconcileAllowlistAsync,
        TimeSpan? interval = null,
        ILogger<FirewallAllowlistReconcileService>? logger = null)
    {
        _reconcileAllowlistAsync = reconcileAllowlistAsync;
        Interval = interval ?? DefaultInterval;
        _logger = logger;
    }

    public TimeSpan Interval { get; }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            do
            {
                try
                {
                    await ReconcileOnceAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger?.LogError(ex, "Firewall allowlist reconcile failed");
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
        }
    }

    public ValueTask ReconcileOnceAsync(CancellationToken cancellationToken)
    {
        return _reconcileAllowlistAsync(cancellationToken);
    }
}
