using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class TribeAnnouncementService(ZoneRegistry zones) : ITribeAnnouncementService
{
    public bool TrySendAnnouncement(PlayerRuntimeState sender, string content)
    {
        // Legacy gate is "if (tTribeRole == 0) return;" -- any non-zero role passes: tribe master (1),
        // sub-master (2), or an elected tribe-council member seated via the tribe-vote mechanism (3).
        // Server/ts25zone/S04_MyWork02.cpp:11496-11500; Server/Header/function.h:92-114 (ReturnTribeRole).
        if (sender.TribeRole == 0)
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
