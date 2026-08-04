using System.Buffers;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Wire;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class DailyResetZoneEventPublisher(ZoneRegistry zones, ILogger<DailyResetZoneEventPublisher> logger)
    : IDailyResetZoneEventPublisher
{
    private const int PayloadSize = 130;

    public ValueTask PublishAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var data = new byte[PayloadSize];
        var response = new ZoneEventInfoResponse
            { Sort = ScheduledZoneCenterEventCodes.DailyResetEventCode, Data = data };

        BroadcastToEveryZone(in response);

        logger.LogInformation("Autonomous daily-reset broadcast sent (sort {Sort})",
            ScheduledZoneCenterEventCodes.DailyResetEventCode);

        return ValueTask.CompletedTask;
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
