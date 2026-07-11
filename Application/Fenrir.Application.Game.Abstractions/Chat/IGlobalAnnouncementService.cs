using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Abstractions.Chat;

public interface IGlobalAnnouncementService
{
    public void TryAnnounce(ZoneClientSession zoneSession, string content);
}
