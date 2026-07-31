using System.Buffers.Binary;
using Fenrir.Cluster.Center.WorldState;
using Fenrir.Data.Abstractions.Guilds;

namespace Fenrir.CenterServer.Hosting;

public sealed class GuildBuffExpiryHost(
    IGuildRepository guilds,
    IWorldEventBroadcaster broadcaster,
    ILogger<GuildBuffExpiryHost> logger) : BackgroundService
{
    private const int GuildBuffExpiredSort = 1133;
    private static readonly TimeSpan DecayInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(DecayInterval);

        do
        {
            try
            {
                await DecayOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Guild-buff decay cycle failed -- next cycle retries");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task DecayOnceAsync(CancellationToken ct)
    {
        var nowEpochSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var all = await guilds.GetAllAsync(ct).ConfigureAwait(false);

        foreach (var guild in all)
        {
            if (guild.BuffType == 0 || guild.BuffTimeForDiff <= 0)
                continue;

            var remaining = RecomputeRemainingMinutes(guild.BuffTimeForDiff, nowEpochSeconds);

            if (remaining <= 0)
            {
                await guilds.SetBuffAsync(guild.GuildId, 0, 0, 0, 0, ct).ConfigureAwait(false);
                await BroadcastRemovalAsync(guild.GuildId, ct).ConfigureAwait(false);
                logger.LogInformation("Guild {GuildId} buff expired and cleared", guild.GuildId);
                continue;
            }

            if (remaining != guild.BuffTime)
                await guilds.SetBuffAsync(guild.GuildId, guild.BuffType, guild.BuffState, remaining,
                    guild.BuffTimeForDiff, ct).ConfigureAwait(false);
        }
    }

    private static int RecomputeRemainingMinutes(long expiryEpochSeconds, long nowEpochSeconds)
    {
        var remainingSeconds = expiryEpochSeconds - nowEpochSeconds;
        return remainingSeconds <= 0 ? 0 : (int)(remainingSeconds / 60);
    }

    private async Task BroadcastRemovalAsync(int guildId, CancellationToken ct)
    {
        var payload = new byte[CenterWorldEventSorts.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), guildId);

        try
        {
            await broadcaster.BroadcastWorldEventAsync(GuildBuffExpiredSort, payload, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Guild-buff removal broadcast failed for guild {GuildId}", guildId);
        }
    }
}
