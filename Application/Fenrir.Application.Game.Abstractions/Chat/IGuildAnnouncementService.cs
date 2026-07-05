using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Chat;

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
    public bool TrySendAnnouncement(PlayerRuntimeState sender, string content);
}
