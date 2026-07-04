using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>CZ_TRIBE_CHAT_SEND (opcode 81) -- zone-local (no inter-zone relay, contracts/02_chat_notices.md), filtered by tribe (alliance not modeled, see <see cref="Zone" />'s ApplyChatCommand remarks).</summary>
public sealed class TribeChatHandler : IInlinePacketHandler<TribeChatRequest>
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
        if (!zone.TryGetPlayer(characterId, out var state) || state is null || state.IsMuted)
            return;

        zone.PostChatCommand(new ChatZoneCommand
        {
            SenderCharacterId = characterId,
            Kind = ChatBroadcastKind.Tribe,
            Content = packet.Content,
            Link = packet.Link
        });
    }
}
