using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_DUEL_START_SEND (opcode 46) -- callable by either accepted side. Scope cut: no
///     ZC_AVATAR_CHANGE_INFO_1 broadcast, no countdown auto-end tick.
/// </summary>
public sealed class DuelStartHandler(IDuelService duelService, ILogger<DuelStartHandler>? logger = null)
    : IInlinePacketHandler<DuelStartRequest>
{
    public void Handle(in DuelStartRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var callerId = zoneSession.CharacterId!.Value;

        logger?.LogDebug("Duel start received: session {SessionId} character {CharacterId}", session.SessionId,
            callerId);

        duelService.Start(callerId);
    }
}
