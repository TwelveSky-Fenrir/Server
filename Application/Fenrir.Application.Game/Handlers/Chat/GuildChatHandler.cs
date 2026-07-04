using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>
///     CZ_GUILD_CHAT_SEND (opcode 77). Empty content ⇒ Quit(); guildless sender ⇒ silent return; muted ⇒
///     silent drop. Fan-out to every guild member across every zone -- the item link IS transported
///     (contrast with party chat, whose link is dead server-side).
/// </summary>
public sealed class GuildChatHandler(ZoneRegistry zones) : IInlinePacketHandler<GuildChatRequest>
{
    public void Handle(in GuildChatRequest packet, IPacketSession session)
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
        if (!zone.TryGetPlayer(characterId, out var sender) || sender is null)
            return;

        if (sender.GuildId is not { } guildId)
            return; // "no guild" -- silent return, not a Quit

        if (sender.IsMuted)
            return;

        var response = new GuildChatResponse
            { AvatarName = sender.Name, Content = packet.Content, Link = packet.Link };

        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            if (recipient.GuildId == guildId)
                recipient.Session.Send(response);
    }
}
