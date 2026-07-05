using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.Guilds;

/// <summary>
///     Periodic real-time countdown for every guild's active buff reserve (<see cref="GuildBuffDecay" />).
///     Guild state spans every <see cref="Zone" /> a member happens to be connected to, so unlike
///     <c>BuffExpirySystem</c> this cannot run as an <see cref="ISimulationSystem" />: that contract
///     is synchronous and scoped to one zone's own tick thread, but decaying a guild-wide row needs an async
///     SQL round trip and must run exactly once process-wide, not once per hosted zone per tick. Same
///     "singleton BackgroundService with its own timer" shape as <c>WorldStateWriteBehindHost</c>.
/// </summary>
public sealed class GuildBuffDecayHost(IGuildRepository guilds, ILogger<GuildBuffDecayHost> logger) : BackgroundService
{
    /// <summary>BuffTime's unit is whole minutes, so sub-minute polling would only ever waste a full-table scan.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await DecayOnceAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    /// <summary>Public, not private: exercised directly by tests instead of waiting on the real timer.</summary>
    public async Task DecayOnceAsync(CancellationToken ct)
    {
        IReadOnlyList<GuildSummaryDto> all;
        try
        {
            all = await guilds.GetAllAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Guild buff decay scan failed -- will retry next interval");
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var guild in all)
        {
            var result = GuildBuffDecay.Apply(guild, now);
            if (!result.Changed)
                continue;

            try
            {
                await guilds.SetBuffAsync(guild.GuildId, result.BuffType, result.BuffState, result.BuffTime,
                    result.BuffTimeForDiff, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Guild {GuildId} buff decay persist failed -- will retry next interval",
                    guild.GuildId);
            }
        }
    }
}
