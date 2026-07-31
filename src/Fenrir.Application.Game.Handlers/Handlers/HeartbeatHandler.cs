using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class HeartbeatHandler(IHeartbeatService service, ILogger<HeartbeatHandler> logger)
    : IInlinePacketHandler<HeartbeatRequest>
{
    public void Handle(in HeartbeatRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CharacterId is not { } characterId || zoneSession.CurrentZone is not Zone zone ||
            !zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

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
