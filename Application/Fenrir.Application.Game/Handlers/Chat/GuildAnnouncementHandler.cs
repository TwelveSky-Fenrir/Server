using Fenrir.Application.Game.Social;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>
///     CZ_GUILD_NOTICE_SEND (opcode 76). Empty content ⇒ Quit(); restricted to the guild MASTER
///     (<c>GuildRoleCodec.IsMaster</c>, DB role 2 -- the wire's own <c>aGuildRole != 0</c> gate, verified
///     against the actual write sites, see <c>GuildRoleCodec</c>'s own remarks) -- a non-master sender is
///     silently ignored, no Quit. No CheckChat/mute gate is documented for this channel. Fan-out to every
///     guild member across every zone, no ItemLinkInfo (a notice, not a chat message).
/// </summary>
public sealed class GuildAnnouncementHandler(ZoneRegistry zones) : IInlinePacketHandler<GuildAnnouncementRequest>
{
    public void Handle(in GuildAnnouncementRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (string.IsNullOrEmpty(packet.Content))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var sender) || sender is null)
            return;

        if (sender.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(sender.GuildRoleDb))
            return;

        var response = new GuildAnnouncementResponse { AvatarName = sender.Name, Content = packet.Content };

        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            if (recipient.GuildId == guildId)
                recipient.Session.Send(response);
    }
}
