using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public sealed class TribeVoteElectionCalendarHost(
    IOptions<GameServerOptions> options,
    ZoneRegistry zoneRegistry,
    TribeVoteElection election,
    ILogger<TribeVoteElectionCalendarHost> logger) : BackgroundService
{
    private readonly bool _testMode = options.Value.VoteTribeTestMode;

    public bool IsArmed { get; } =
        zoneRegistry.TryGet(options.Value.VoteTribeMapId, out _) && options.Value.VoteTribeEnabled;

    public async ValueTask TickAsync(DateTime localNow, CancellationToken ct)
    {
        if (!IsArmed)
            return;

        var transition = TribeVoteElectionCalendar.Evaluate(election.Phase, localNow.Day, localNow.Hour, _testMode);

        switch (transition)
        {
            case TribeVoteCalendarTransition.OpenRegistration:
                await election.OpenCandidacyWindowAsync(ct).ConfigureAwait(false);
                break;
            case TribeVoteCalendarTransition.OpenVoting:
                election.OpenVotingWindow();
                break;
            case TribeVoteCalendarTransition.CloseVoting:
                election.CloseVotingWindow();
                break;
            case TribeVoteCalendarTransition.AnnounceResults:
                await election.AnnounceResultsAsync(ct).ConfigureAwait(false);
                break;
            case TribeVoteCalendarTransition.ResetToIdle:
                await election.ResetToIdleAsync(ct).ConfigureAwait(false);
                break;
            case TribeVoteCalendarTransition.None:
            default:
                break;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsArmed)
        {
            logger.LogInformation(
                "TribeVoteElectionCalendarHost is inert on this shard (designated map {MapId} not hosted here, or VoteTribeEnabled={VoteTribeEnabled})",
                options.Value.VoteTribeMapId, options.Value.VoteTribeEnabled);
            return;
        }

        using var timer = new PeriodicTimer(SimulationClock.LegacyTick);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                try
                {
                    await TickAsync(DateTime.Now, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Tribe Vote election calendar tick failed");
                }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
