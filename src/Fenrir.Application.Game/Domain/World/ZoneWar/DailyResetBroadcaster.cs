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

        // Le legacy remet uRewardClaimDay a zero UNIQUEMENT le lundi et uRewardClaimState tous les jours
        // (Server/ts25center/S07_MyGame01.cpp:227-233). Le jour se lit sur l'instant qui a declenche, pas
        // sur un second appel a l'horloge.
        var clearWeeklyDayCounter = localNow.DayOfWeek == DayOfWeek.Monday;

        try
        {
            await dailyRewardReset.ResetDailyRewardClaimsAsync(clearWeeklyDayCounter, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Sans ce log, un arret tombant pendant l'UPDATE ne laisse AUCUNE trace : le catch de l'hote
            // filtre deja l'annulation.
            logger.LogWarning(
                "Daily reward-claim reset cancelled mid-flight during shutdown -- not marked as fired, the next " +
                "start retries at the next 00:01 local");
            throw;
        }
        catch (Exception ex)
        {
            // On NE marque PAS le jour comme tire : les ticks restants de la minute (poll 15 s) reessaient.
            // On ne diffuse pas non plus -- annoncer un reset qui n'a pas eu lieu est pire que le silence.
            logger.LogCritical(ex,
                "Daily reward-claim reset FAILED (clearWeeklyDayCounter {ClearWeekly}) -- players cannot claim " +
                "today's reward; retrying within this minute", clearWeeklyDayCounter);
            return;
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
