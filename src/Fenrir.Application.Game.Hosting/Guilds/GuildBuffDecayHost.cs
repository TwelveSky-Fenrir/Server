using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.Guilds;

public sealed class GuildBuffDecayHost(
    IGuildRepository guilds,
    ZoneRegistry zones,
    IGuildBuffExpiryRelayQueue expiryRelay,
    IOptions<GameServerOptions> options,
    ILogger<GuildBuffDecayHost> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

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
        }
    }

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

        var now = DateTimeOffset.UtcNow;
        foreach (var guild in all)
        {
            var result = GuildBuffDecay.Apply(guild, now);
            if (!result.Changed)
                continue;

            try
            {
                await guilds.SetBuffTimeAsync(guild.GuildId, result.BuffTime, result.BuffTimeForDiff, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Guild {GuildId} buff decay persist failed -- will retry next interval",
                    guild.GuildId);
                continue;
            }

            if (result.BuffTime >= 1)
                continue;

            PushExpiry(guild.GuildId, result.BuffTime);
        }
    }

    private void PushExpiry(int guildId, int newBuffTime)
    {
        var command = new GuildBuffExpiryZoneCommand(guildId, newBuffTime);
        foreach (var zone in zones.Zones)
            zone.PostGuildBuffExpiryCommand(in command);

        expiryRelay.Enqueue(new GuildBuffExpiryRelayEntry(options.Value.ShardId, guildId, newBuffTime));
    }
}
