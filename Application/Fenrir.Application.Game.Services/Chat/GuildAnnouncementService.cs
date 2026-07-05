using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class GuildAnnouncementService(ZoneRegistry zones) : IGuildAnnouncementService
{
    public bool TrySendAnnouncement(PlayerRuntimeState sender, string content)
    {
        if (sender.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(sender.GuildRoleDb))
            return false;

        var response = new GuildAnnouncementResponse { AvatarName = sender.Name, Content = content };

        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            if (recipient.GuildId == guildId)
                recipient.Session.Send(response);

        return true;
    }
}
