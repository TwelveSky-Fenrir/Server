using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Chat;

public interface IGuildAnnouncementService
{

        public bool TrySendAnnouncement(PlayerRuntimeState sender, string content);
}
