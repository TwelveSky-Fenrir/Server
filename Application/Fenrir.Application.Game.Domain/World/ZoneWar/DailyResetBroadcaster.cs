using System.Buffers;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class DailyResetBroadcaster(ZoneRegistry zones, ILogger<DailyResetBroadcaster> logger)
{
    private const int PayloadSize = 130;

    private readonly DailyResetBroadcastScheduler _scheduler = new();

    public void Tick(DateTime utcNow)
    {
        if (_scheduler.TryConsumeDueFire(utcNow))
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
                    if (player.Session is ClientSession clientSession)
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
