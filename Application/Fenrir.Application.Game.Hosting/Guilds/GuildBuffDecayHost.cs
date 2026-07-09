using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.Guilds;

/// <summary>
///     Periodic real-time countdown for every guild's active buff reserve (<see cref="GuildBuffDecay" />).
///     Guild state spans every <see cref="Zone" /> a member happens to be connected to, so unlike
///     <c>BuffExpirySystem</c> this cannot run as an <see cref="ISimulationSystem" />: that contract
///     is synchronous and scoped to one zone's own tick thread, but decaying a guild-wide row needs an async
///     SQL round trip and must run exactly once process-wide, not once per hosted zone per tick. Same
///     "singleton BackgroundService with its own timer" shape as <c>WorldStateWriteBehindHost</c>.
/// </summary>
/// <remarks>
///     Also owns the guild-buff-reserve-exhaustion immediate strip-effect push's own trigger detection
///     (Server/ts25center/S07_MyGame01.cpp:291-331's edge-triggered <c>gBuffTime &lt; 1</c> branch) -- see
///     <see cref="Zone.ApplyGuildBuffExpiryCommand" /> and <see cref="GuildBuffExpiryRelayHost" /> for the
///     same-shard/cross-shard delivery halves this host only detects and dispatches, never delivers directly
///     itself.
/// </remarks>
public sealed class GuildBuffDecayHost(
    IGuildRepository guilds,
    ZoneRegistry zones,
    IGuildBuffExpiryRelayQueue expiryRelay,
    IOptions<GameServerOptions> options,
    ILogger<GuildBuffDecayHost> logger) : BackgroundService
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
                continue;
            }

            // Edge-triggered reserve-exhaustion push (Server/ts25center/S07_MyGame01.cpp:316-330's own
            // gBuffTime < 1 branch) -- the eligibility query above structurally never re-selects a guild whose
            // reserve is already exactly zero (GuildBuffDecay.Apply's own <= 0 short-circuit), so this fires
            // exactly once, at the pass the reserve first reaches exhaustion.
            if (result.BuffTime >= 1)
                continue;

            PushExpiry(guild.GuildId, result.BuffTime);
        }
    }

    /// <summary>
    ///     Same-shard delivery first (synchronous, posted directly onto every zone THIS shard hosts), then
    ///     cross-shard fan-out via <see cref="IGuildBuffExpiryRelayQueue" /> so every OTHER live shard's own
    ///     <see cref="GuildBuffExpiryRelayHost" /> delivers to its own locally-hosted matches -- same ordering
    ///     as every other cluster-wide broadcast producer in this codebase (e.g. <c>GuildAnnouncementService</c>).
    /// </summary>
    private void PushExpiry(int guildId, int newBuffTime)
    {
        var command = new GuildBuffExpiryZoneCommand(guildId, newBuffTime);
        foreach (var zone in zones.Zones)
            zone.PostGuildBuffExpiryCommand(in command);

        expiryRelay.Enqueue(new GuildBuffExpiryRelayEntry(options.Value.ShardId, guildId, newBuffTime));
    }
}
