using System.Buffers.Binary;
using Fenrir.Cluster.WorldState;
using Fenrir.Data.Abstractions.Guilds;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.CenterServer.Hosting;

/// <summary>
///     The ~10s guild-buff decay cadence: for every guild with an active buff, recomputes the remaining minutes
///     as the wall-clock difference between the buff's absolute expiry timestamp and now, persists the new
///     remaining time, and clears + broadcasts a removal when it has expired.
/// </summary>
/// <remarks>
///     Reimplemented from the 10-beat guild-buff decay (Server/ts25center/S07_MyGame01.cpp:270-274,291-331) and
///     the remaining-minutes formula (Server/Header/CSQLGuild.cpp:221-230): <c>remaining = (expiry - now) / 60</c>.
///     Operates on the guild table, not one of the five aggregates, via <see cref="IGuildRepository" /> (reuses
///     the existing <c>SetBuffAsync</c> proc; no new schema). Cadence is wall-clock-derived (PeriodicTimer).
///     <para>
///         ASSUMPTION: <c>GuildSummaryDto.BuffTimeForDiff</c> is the absolute expiry as Unix epoch seconds and
///         <c>BuffTime</c> is the remaining minutes (matching the legacy <c>difftime</c>/60 shape). If the stored
///         encoding differs, adjust <see cref="RecomputeRemainingMinutes" />.
///         FLAG: the guild-buff-expired op33 removal-notice sort code is not cited in the contract
///         (<see cref="GuildBuffExpiredSort" />); it is emitted so the pipeline is wired end-to-end, but the
///         exact value needs confirmation from cpp-zone-gameplay-analyst. A wrong sort is dropped safely by
///         zones (op33 is a fixed-size envelope), so this cannot desync the wire.
///     </para>
/// </remarks>
public sealed class GuildBuffExpiryHost(
    IGuildRepository guilds,
    ICenterLinkBroadcaster broadcaster,
    ILogger<GuildBuffExpiryHost> logger) : BackgroundService
{
    private static readonly TimeSpan DecayInterval = TimeSpan.FromSeconds(10);

    // FLAG (uncited): placeholder for the guild-buff-expired notice sort. Needs confirmation.
    private const int GuildBuffExpiredSort = 1133;

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
