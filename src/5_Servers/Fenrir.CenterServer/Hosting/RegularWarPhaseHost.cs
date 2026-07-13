using Fenrir.Cluster.WorldState;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.CenterServer.Hosting;

/// <summary>
/// Real-time driver for the Center-authoritative Regular War (Zone049) phase machine. A single
/// <see cref="BackgroundService"/> that beats a <see cref="PeriodicTimer"/> and asks
/// <see cref="RegularWarPhaseAuthority"/> to advance every RW instance to whatever phase its wall-clock
/// schedule currently dictates.
/// </summary>
/// <remarks>
/// Cadence is a genuine wall-clock timer, never a tick/iteration counter — the RW schedule must not skew if
/// the host stalls or pauses. The 1 s beat matches legacy <c>ts25center</c>'s own ~1 Hz logic beat
/// (<c>TimeLogic = 1000</c>) and is far finer than the shortest RW phase (Closing, 6 s), so per-phase
/// overshoot is bounded to ~1 s against phases measured in minutes. The authority is itself catch-up safe, so
/// a coalesced/missed beat under load is corrected on the next tick from the true elapsed instant.
/// <para>
/// DORMANT this lot: the machine runs but shards still author world state. See
/// <see cref="RegularWarPhaseAuthority"/> for the deliberate shard→center authority divergence this
/// implements.
/// </para>
/// </remarks>
internal sealed class RegularWarPhaseHost(
    RegularWarPhaseAuthority authority,
    ILogger<RegularWarPhaseHost> logger,
    TimeProvider? timeProvider = null)
    : BackgroundService
{
    private static readonly TimeSpan BeatInterval = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        authority.Initialize();

        logger.LogInformation(
            "RegularWarPhaseHost started: driving the Center-authoritative Regular War phase machine at a {Beat} " +
            "wall-clock beat.",
            BeatInterval);

        using var timer = new PeriodicTimer(BeatInterval, _timeProvider);

        do
        {
            try
            {
                authority.Advance(_timeProvider.GetUtcNow());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Backstop only: the authority already isolates faults per instance. This guards the beat
                // bookkeeping itself so one bad tick never stops the host.
                logger.LogError(ex, "RegularWarPhaseHost beat faulted; the next beat still runs.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
