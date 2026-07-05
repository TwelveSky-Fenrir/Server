using Fenrir.Application.Game.World;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Chat.Services;

/// <summary>
///     Business logic for CZ_GUILD_CHAT_SEND (opcode 77): guildless/muted gating plus the process-wide fan-out
///     to every member of the sender's guild. Unlike party chat, the item link is genuinely relayed here.
/// </summary>
public interface IGuildChatService
{
    /// <summary>
    ///     Broadcasts <paramref name="content" />/<paramref name="link" /> to every online member of
    ///     <paramref name="sender" />'s guild. A guildless or muted sender is silently ignored -- returns false.
    /// </summary>
    bool TrySendChat(PlayerRuntimeState sender, string content, ItemLinkInfo link);
}

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
