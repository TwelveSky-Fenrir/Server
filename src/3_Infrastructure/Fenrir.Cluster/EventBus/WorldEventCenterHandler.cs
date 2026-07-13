using Fenrir.Cluster.Wire.Packets;
using Fenrir.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.EventBus;

/// <summary>
/// Handler du bus d'events monde (op33) côté CenterServer : reçoit une <see cref="WorldEventInbound"/> poussée
/// par une Zone authentifiée.
/// </summary>
/// <remarks>
/// TODO(F4/E2) : ce corps est le <b>substrat de dispatch</b> — il prouve la chaîne wire S2S (framing 1 octet →
/// gate d'état → dispatch). L'ingesteur autoritaire réel (effet d'état monde D'ABORD via une allowlist de
/// <c>tSort</c> — DROP+audit de l'inconnu —, PUIS fan-out <see cref="WorldEventOutbound"/> aux Zones, exceptions
/// 9998 hero-rank / 10000 close-proxy sans fan-out) est livré au Lot F4/E2 par-dessus ce point d'entrée.
/// </remarks>
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
