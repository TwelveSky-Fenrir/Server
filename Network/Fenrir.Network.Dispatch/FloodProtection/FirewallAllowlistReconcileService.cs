using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch.FloodProtection;

/// <summary>
///     The application-layer analog of legacy <c>ts25firewall</c>'s periodic <c>RemoveIPTick</c> reconcile
///     (<c>Server/ts25firewall/main.cpp:682-698</c>), driven off a fixed wall-clock cadence exactly like every
///     other Fenrir maintenance sweep (<c>SessionTicketPurgeHost</c>/<c>AccountSessionReapHost</c>): it triggers a
///     single, self-contained reconcile once at startup and then once per <see cref="Interval" /> for the life of
///     the process. This class owns only the <em>cadence and resilience</em> half of that behavior; the actual
///     database reconcile is supplied as an injected delegate so this Network-layer type never takes a dependency
///     on <c>Fenrir.Data</c> -- the same delegate-injection posture <see cref="IpFloodGuard" /> already uses for
///     its own block-IP write path.
/// </summary>
/// <remarks>
///     <para>
///         <b>It is an allowlist reconcile, not a ban expiry.</b> The requesting finding framed this as "firewall
///         IP bans never expire (no ~60s RemoveIPTick reconciliation)", but the verified behavior contract
///         corrects that on two counts, and this type is named/documented to the correction rather than the
///         finding: (1) <c>RemoveIPTick</c> reconciles the <c>type</c>-0 (<c>TCP_ALLOW</c>) <em>allowlist</em> --
///         it prunes allow rows whose IP no longer maps to a current member, then re-seeds the fixed
///         infrastructure IPs and every current member IP (<c>Server/ts25firewall/firewall.h:20-27</c>,
///         <c>main.cpp:684-698</c>). There is no per-IP ban list in legacy at all: the per-IP <c>TCP_BLOCK</c>
///         init is commented out and blocking is a default-deny <c>ANY_BLOCK</c> with allowlists layered on top
///         (<c>main.cpp:628,:636,:643</c>). (2) The cadence is <b>~120 seconds</b>, not ~60: the reconcile fires
///         when the tick counter reaches <c>24*2</c> (48) ticks and each tick is a 2.5-second sleep, so the
///         reconcile period is double what the source's own stale comment table claims
///         (<c>main.cpp:558,:654-659,:803-821</c>).
///     </para>
///     <para>
///         <b>Reconcile atomicity is the delegate's responsibility.</b> Legacy issues the prune + two re-seed
///         steps as three separate database statements with no surrounding transaction, so a failed statement
///         silently leaves the allowlist stale until the next reconcile (<c>main.cpp:684-698</c>). Per the
///         project invariant that a multi-row mutation is pushed into the stored procedure, the injected
///         delegate performs whichever of those steps it implements as one atomic reconcile (a single
///         procedure call); this service only guarantees the delegate is invoked on the correct cadence and
///         that a thrown reconcile is logged and retried on the next tick rather than crashing the host --
///         exactly the "a missed sweep just delays the next reconcile" posture of the sibling maintenance
///         sweeps. Workstream D3's real delegate (<c>IFirewallRuleRepository.ReconcileAllowlistAsync</c>)
///         deliberately implements only the prune step -- see that method's own remarks for why the two
///         re-seed steps cannot be faithfully reproduced without inventing data Fenrir's schema doesn't have.
///     </para>
///     <para>
///         The legacy Cloudflare-allow path (<c>TCP_ALLOW_CF</c>) and its <c>WebFireWall</c> HTTP thread are dead
///         code (gated on <c>USE_FIREWALL_WEB</c>, never defined in any build -- <c>main.cpp:647-651,:795-797</c>)
///         and are deliberately not reproduced.
///     </para>
/// </remarks>
public sealed class FirewallAllowlistReconcileService
{
    /// <summary>
    ///     ~120 seconds (48 ticks × 2.5s), the corrected legacy reconcile period -- see the class remarks for why
    ///     the finding's "~60s" understates it by half.
    /// </summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(120);

    private readonly ILogger<FirewallAllowlistReconcileService>? _logger;

    /// <summary>The whole atomic reconcile (prune stale allow rows, re-seed infrastructure + member IPs) as one call.</summary>
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

    /// <summary>
    ///     Runs the reconcile once immediately (mirroring legacy's own startup <c>Logic()</c> call before the tick
    ///     loop, <c>Server/ts25firewall/main.cpp:645</c>), then once per <see cref="Interval" /> until
    ///     <paramref name="cancellationToken" /> is cancelled. A reconcile that throws is logged and retried on the
    ///     next tick -- never propagated -- so a transient database failure can never bring down the host driving
    ///     this loop; only cancellation ends it, cleanly.
    /// </summary>
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
                    // A missed reconcile just leaves the allowlist stale for one more cycle -- never worth
                    // crashing the process over, same posture as SessionTicketPurgeHost/AccountSessionReapHost.
                    _logger?.LogError(ex, "Firewall allowlist reconcile failed");
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown: WaitForNextTickAsync observed the token being cancelled (it surfaces cancellation
            // as OperationCanceledException, per its documented contract), or a reconcile itself honoured the
            // token mid-flight. Return cleanly rather than surfacing shutdown as a fault to the driving host.
        }
    }

    /// <summary>
    ///     One reconcile step -- public so a host wrapper can force an out-of-band reconcile and so the cadence is
    ///     unit-testable without waiting on the real interval. Delegates the whole atomic three-step reconcile to
    ///     the injected implementation; see the class remarks for why atomicity lives there, not here.
    /// </summary>
    public ValueTask ReconcileOnceAsync(CancellationToken cancellationToken)
    {
        return _reconcileAllowlistAsync(cancellationToken);
    }
}
