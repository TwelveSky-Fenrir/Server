using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class GuildChatService(ZoneRegistry zones) : IGuildChatService
{
    public bool TrySendChat(PlayerRuntimeState sender, string content, ItemLinkInfo link)
    {
        if (sender.GuildId is not { } guildId)
            return false;

        if (sender.IsMuted)
            return false;

        var response = new GuildChatResponse { AvatarName = sender.Name, Content = content, Link = link };

        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            if (recipient.GuildId == guildId)
                recipient.Session.Send(response);

        return true;
    }
}
