using System.Buffers;
using Fenrir.Core.Wire;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class DailyResetBroadcaster(
    ZoneRegistry zones,
    IDailyRewardResetRepository dailyRewardReset,
    ILogger<DailyResetBroadcaster> logger)
{
    private const int PayloadSize = 130;

    private readonly DailyResetBroadcastScheduler _scheduler = new();

    public async ValueTask TickAsync(DateTimeOffset localNow, CancellationToken ct)
    {
        if (!_scheduler.IsDue(localNow))
            return;

        if (_scheduler.AllowsEagerReset(localNow))
        {
            var clearWeeklyDayCounter = localNow.DayOfWeek == DayOfWeek.Monday;

            try
            {
                await dailyRewardReset.ResetDailyRewardClaimsAsync(clearWeeklyDayCounter, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning(
                    "Daily reward-claim reset cancelled mid-flight during shutdown -- not marked as fired, the " +
                    "next start retries as soon as it boots inside the reset window");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex,
                    "Daily reward-claim reset FAILED (clearWeeklyDayCounter {ClearWeekly}) -- retrying until the " +
                    "reset window closes, after which the lazy per-character path is the only fallback",
                    clearWeeklyDayCounter);
                return;
            }
        }
        else
        {
            logger.LogWarning(
                "Daily reset for {LocalDate} fired late at {LocalTime} (process down or stalled at 00:01) -- " +
                "broadcasting only, the bulk reward-claim reset is deliberately skipped",
                DateOnly.FromDateTime(localNow.DateTime), localNow.TimeOfDay);
        }

        _scheduler.MarkFired(localNow);
        Broadcast();
    }

    private void Broadcast()
    {
        var data = new byte[PayloadSize];
        var response = new ZoneEventInfoResponse
            { Sort = ScheduledZoneCenterEventCodes.DailyResetEventCode, Data = data };

        BroadcastToEveryZone(in response);

        logger.LogInformation("Autonomous daily-reset broadcast sent (sort {Sort})",
            ScheduledZoneCenterEventCodes.DailyResetEventCode);
    }

    private void BroadcastToEveryZone<TPacket>(in TPacket response) where TPacket : struct, IOutgoingPacket
    {
        var total = FrameWriter.FrameSizeOf<TPacket>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            foreach (var zone in zones.Zones)
            foreach (var player in zone.Players)
                try
                {
                    if (player.Session is { } clientSession)
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Daily-reset broadcast to character {RecipientId} (zone {MapId}) failed",
                        player.CharacterId, zone.MapId);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
