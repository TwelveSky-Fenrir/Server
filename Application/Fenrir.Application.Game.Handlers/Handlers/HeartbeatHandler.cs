using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     CZ_HEARTBEAT_SEND (op151). Rejects a replayed frame (same <c>LastSend</c> counter as the previous
///     accepted heartbeat) and stamps <see cref="PlayerRuntimeState.LastSentHeartbeat" /> so
///     <see cref="ZoneReadyHandler" />'s watchdog can later detect a session that has gone quiet.
/// </summary>
/// <remarks>
///     CLIENT.h:501-505 pairs <c>LastSend</c> with a 32-byte CRC blob checked against <c>0xCAFEBABE</c> --
///     but only when <c>uUserSort==0 &amp;&amp; !DEV_SERVER</c>, and the reference build the wire contract was
///     extracted from (M1_Legacy_Wire_Contract.md §5.8) has <c>DEV_SERVER</c> defined, i.e. that check is
///     compiled out of the very binary this port is based on. Deliberately not modeled here for that reason,
///     not an oversight.
/// </remarks>
public sealed class HeartbeatHandler(IHeartbeatService service, ILogger<HeartbeatHandler> logger)
    : IInlinePacketHandler<HeartbeatRequest>
{
    public void Handle(in HeartbeatRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CharacterId is not { } characterId || zoneSession.CurrentZone is not Zone zone ||
            !zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        // Fires periodically for every connected session (independent of PacketLog's own per-packet Debug
        // logging, which already records the raw receive) -- gate behind IsEnabled so a hand-written LogDebug
        // never boxes args across the whole connected population when Debug is disabled.
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId}: HeartbeatRequest (op151) received for character {CharacterId}, lastSend {LastSend}",
                session.SessionId, characterId, packet.LastSend);

        if (service.Process(state, packet.LastSend) == HeartbeatOutcome.Replayed)
        {
            logger.LogWarning(
                "Heartbeat replay detected for character {CharacterId} (session {SessionId}): lastSend {LastSend} -- aborting session",
                characterId, session.SessionId, packet.LastSend);
            zoneSession.Abort(DisconnectReason.Faulted);
        }
    }
}
