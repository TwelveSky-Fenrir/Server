using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

/// <summary>CZ_TRIBE_CHAT_SEND (opcode 81) -- zone-local only, no inter-zone relay; alliance not modeled.</summary>
public sealed class TribeChatHandler(ITribeChatService tribeChatService, ILogger<TribeChatHandler>? logger = null)
    : IInlinePacketHandler<TribeChatRequest>
{
    public void Handle(in TribeChatRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug(
            "Session {SessionId}: CZ_TRIBE_CHAT_SEND received (character {CharacterId}, content length {ContentLength})",
            session.SessionId, zoneSession.CharacterId, packet.Content.Length);

        if (ChatRouter.IsContentEmpty(packet.Content))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        tribeChatService.TryPostChat(zone, state, packet.Content, packet.Link);
    }
}
