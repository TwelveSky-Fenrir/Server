using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>
///     CZ_GENERAL_CHAT_SEND (opcode 38). A muted sender is silently dropped, not disconnected. GM
///     inline-command interception is not modeled -- no GM-rank concept exists yet.
/// </summary>
public sealed class LocalChatHandler : IInlinePacketHandler<LocalChatRequest>
{
    public void Handle(in LocalChatRequest packet, IPacketSession session)
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
            Kind = ChatBroadcastKind.Local,
            Content = packet.Content,
            Link = packet.Link
        });
    }
}
