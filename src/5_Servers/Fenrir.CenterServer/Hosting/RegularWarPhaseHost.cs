using Fenrir.Cluster.WorldState;

namespace Fenrir.CenterServer.Hosting;

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
                logger.LogError(ex, "RegularWarPhaseHost beat faulted; the next beat still runs.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
