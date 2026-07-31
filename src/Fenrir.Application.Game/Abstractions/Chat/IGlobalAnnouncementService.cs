using Fenrir.Application.Game.Sessions;

namespace Fenrir.Application.Game.Abstractions.Chat;

public interface IGlobalAnnouncementService
{
    public void TryAnnounce(ZoneClientSession zoneSession, string content);
}
