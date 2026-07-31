using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Abstractions.Chat;

public interface IGlobalAnnouncementService
{
    public void TryAnnounce(IZoneSession zoneSession, string content);
}
