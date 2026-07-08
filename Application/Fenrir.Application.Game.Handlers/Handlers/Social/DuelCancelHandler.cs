using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>CZ_DUEL_CANCEL_SEND (opcode 44) -- the challenger withdraws their own still-pending ask.</summary>
public sealed class DuelCancelHandler(IDuelService duelService, ILogger<DuelCancelHandler>? logger = null)
    : IInlinePacketHandler<DuelCancelRequest>
{
    public void Handle(in DuelCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var challengerId = zoneSession.CharacterId!.Value;

        logger?.LogDebug("Duel cancel received: session {SessionId} character {CharacterId}", session.SessionId,
            challengerId);

        duelService.Cancel(challengerId);
    }
}
