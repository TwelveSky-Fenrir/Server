using Fenrir.Application.Game;

namespace Fenrir.Application.Game.Abstractions.Chat;

public interface IGlobalAnnouncementService
{
    public void TryAnnounce(ZoneClientSession zoneSession, string content);
}
