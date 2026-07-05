using Fenrir.Application.Game.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Chat.Services;

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
    bool TrySendAnnouncement(PlayerRuntimeState sender, string content);
}

public sealed class TribeAnnouncementService(ZoneRegistry zones) : ITribeAnnouncementService
{
    public bool TrySendAnnouncement(PlayerRuntimeState sender, string content)
    {
        if (sender.TribeRole is not (1 or 2))
            return false;

        var response = new TribeAnnouncementResponse
            { TribeRole = sender.TribeRole, AvatarName = sender.Name, Content = content };

        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            if (recipient.Tribe == sender.Tribe)
                recipient.Session.Send(response);

        return true;
    }
}
