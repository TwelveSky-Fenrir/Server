using Fenrir.Cluster.Wire.Packets;
using Fenrir.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.EventBus;

/// <summary>
///     The op33 substrate handler: the packet-dispatch entry point for <see cref="WorldEventInbound" />. The
///     "emitter must be an authenticated zone" precondition (legacy <c>mCheckServerType != SERVER_ZONE -&gt; Quit</c>,
///     <c>S04_MyWork02.cpp:162-166</c>) is enforced upstream by the packet's <c>AllowedStates = [Authenticated]</c>
///     session-state gate; a non-authenticated link never reaches here. All real work (allowlist gate, effect-of-state,
///     fan-out, AntiCheat audit) is delegated to <see cref="CenterWorldEventIngestor" />.
/// </summary>
public sealed class WorldEventCenterHandler(
    CenterWorldEventIngestor ingestor,
    ILogger<WorldEventCenterHandler> logger)
    : IAsyncPacketHandler<WorldEventInbound>
{
    public ValueTask HandleAsync(WorldEventInbound packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("CenterServer received world-event op33 (tSort {Sort}) from S2S link {SessionId}",
            packet.Sort, session.SessionId);
        return ingestor.IngestAsync(packet, session.SessionId, cancellationToken);
    }
}
