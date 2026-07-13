using Fenrir.Cluster.Wire.Packets;
using Fenrir.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.EventBus;

public sealed class WorldEventCenterHandler(ILogger<WorldEventCenterHandler> logger)
    : IAsyncPacketHandler<WorldEventInbound>
{
    public ValueTask HandleAsync(WorldEventInbound packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("CenterServer received world-event op33 (tSort {Sort}) from S2S link {SessionId}",
            packet.Sort, session.SessionId);
        return ValueTask.CompletedTask;
    }
}
