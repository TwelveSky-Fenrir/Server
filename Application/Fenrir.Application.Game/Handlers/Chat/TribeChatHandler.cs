using Fenrir.Application.Game.Handlers.Chat.Services;
using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>CZ_TRIBE_CHAT_SEND (opcode 81) -- zone-local only, no inter-zone relay; alliance not modeled.</summary>
public sealed class TribeChatHandler(ITribeChatService tribeChatService) : IInlinePacketHandler<TribeChatRequest>
{
    public void Handle(in TribeChatRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

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
