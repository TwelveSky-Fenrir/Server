using System.Buffers;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public partial class Zone
{
    private const int TowerInfoPushLegacyTicks = 60;

    private int _towerInfoPushAccrualTicks;

    internal void TickTowerInfoPush(int legacyTicksElapsed)
    {
        if (towerWar is null || legacyTicksElapsed <= 0)
            return;

        if (TowerZoneIndexTable.GetTowerIndex(MapId) < 0)
            return;

        _towerInfoPushAccrualTicks += legacyTicksElapsed;
        if (_towerInfoPushAccrualTicks < TowerInfoPushLegacyTicks)
            return;

        _towerInfoPushAccrualTicks = 0;

        if (_players.IsEmpty)
            return;

        var response = towerWar.BuildStatusSnapshot();

        var total = FrameWriter.FrameSizeOf<TowerStatusResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            foreach (var player in _players.Values)
                try
                {
                    if (TryGetZoneWideBroadcastRecipient(player.CharacterId, out var clientSession))
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} periodic tower-info push to character {RecipientId} failed",
                        MapId, player.CharacterId);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
