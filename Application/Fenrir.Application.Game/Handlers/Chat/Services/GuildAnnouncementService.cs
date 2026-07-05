using Fenrir.Application.Game.Social;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Chat.Services;

/// <summary>
///     Business logic for CZ_GUILD_NOTICE_SEND (opcode 76): guild-master authorization gate plus the
///     process-wide fan-out to every member of the sender's guild.
/// </summary>
public interface IGuildAnnouncementService
{
    /// <summary>
    ///     Broadcasts <paramref name="content" /> to every online member of <paramref name="sender" />'s guild,
    ///     provided the sender is the guild master. A non-master sender (or guildless sender) is silently
    ///     ignored -- returns false, no exception, matching the legacy's own silent-drop posture.
    /// </summary>
    bool TrySendAnnouncement(PlayerRuntimeState sender, string content);
}

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
