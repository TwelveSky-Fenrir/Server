using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Chat;

/// <summary>
///     Business logic for CZ_TRIBE_NOTICE_SEND (opcode 80): tribe master/sub-master authorization gate
///     (<see cref="PlayerRuntimeState.TribeRole" /> 1 or 2) plus the process-wide fan-out to every member of
///     the sender's tribe. Strict tribe match only, no alliance.
/// </summary>
public interface ITribeAnnouncementService
{
    /// <summary>
    ///     Broadcasts <paramref name="content" /> to every online member of <paramref name="sender" />'s tribe,
    ///     provided the sender is tribe master or sub-master. A regular member is silently ignored -- returns
    ///     false.
    /// </summary>
    public bool TrySendAnnouncement(PlayerRuntimeState sender, string content);
}
