using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
///     Per-tick driver for <see cref="TribeVoteElectionCalendar" />: reads the real calendar day/hour every
///     zone-tick and applies whichever of <see cref="TribeVoteElection" />'s transitions the calendar decides,
///     but only on the one shard configured as server number 37 with <c>VoteTribe</c> enabled -- every other
///     shard's instance of this host is permanently inert.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:612-616 -- the server-number-37 + <c>VoteTribe</c> arm
///     gate, read once at zone startup and cached (<see cref="_armed" />/<see cref="_testMode" />), matching
///     the legacy's own once-at-boot INI read. See <see cref="TribeVoteElectionCalendar" />'s own remarks for
///     why, in production, this host only ever calls <see cref="TribeVoteElection.OpenCandidacyWindowAsync" />.
/// </remarks>
public sealed class TribeVoteElectionCalendarHost(
    IOptions<GameServerOptions> options,
    TribeVoteElection election,
    ILogger<TribeVoteElectionCalendarHost> logger) : BackgroundService
{
    /// <summary>Server/ts25zone/S07_MyGame01.cpp:612-616 -- the one physical instance this scheduler ever arms on.</summary>
    public const int DesignatedShardId = 37;

    private readonly bool _armed = options.Value.ShardId == DesignatedShardId && options.Value.VoteTribeEnabled;
    private readonly bool _testMode = options.Value.VoteTribeTestMode;

    /// <summary>True once at construction if this shard is both server number 37 and has VoteTribe enabled.</summary>
    public bool IsArmed => _armed;

    /// <summary>
    ///     Evaluates and, if due, applies one calendar transition -- pure enough to drive directly from a test
    ///     without a real timer, same convention as <see cref="Fenrir.Application.Game.Domain.World.ZoneWar.ZoneEventBroadcaster" />'s
    ///     sibling <c>ZoneWarTickService.Tick</c>. A no-op entirely if this instance is not armed.
    /// </summary>
    public async ValueTask TickAsync(DateTime utcNow, CancellationToken ct)
    {
        if (!_armed)
            return;

        var transition = TribeVoteElectionCalendar.Evaluate(election.Phase, utcNow.Day, utcNow.Hour, _testMode);

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
        if (!_armed)
        {
            logger.LogInformation(
                "TribeVoteElectionCalendarHost is inert on this shard (ShardId={ShardId}, VoteTribeEnabled={VoteTribeEnabled})",
                options.Value.ShardId, options.Value.VoteTribeEnabled);
            return;
        }

        using var timer = new PeriodicTimer(SimulationClock.LegacyTick);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await TickAsync(DateTime.UtcNow, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }
}
